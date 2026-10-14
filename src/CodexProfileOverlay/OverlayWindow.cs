using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CodexProfileOverlay;

internal sealed partial class OverlayWindow : Window
{
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(150);
    // Match the full Codex title bar (--height-titlebar: 4px * 11),
    // including the space around its smaller application-menu controls.
    private const double HeaderHeight = 44;
    private const double HeaderButtonHeight = 40;
    private static readonly FontFamily EmojiFont = new("Segoe UI Emoji");
    private readonly OverlaySettings settings;
    private readonly SafeLogger logger;
    private readonly OverlayLayoutService layoutService = new();
    private readonly Border shell = new();
    private readonly Popup compactPopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Bottom };
    private readonly List<Button> profileButtons = [];
    private readonly UsageHoverController usageHover = new();
    private HwndSource? hwndSource;
    private IReadOnlyList<ProfileInfo> profiles = [];
    private string? activeProfile;
    private string? recommendedProfile;
    private ProfileStatusDocument? statusDocument;
    private IntPtr ownerHwnd;
    private bool isDragging;
    private bool isBallMinimized;
    private Point ballOffset;
    private bool quotaRebuildPending;
    private bool dragPending;
    private bool isSwitching;
    private Point dragOffset;
    private Point dragStartScreen;
    private double dragFixedWidth;
    private double? draggedLogicalWidth;
    private double draggedScale;
    private OverlayDisplayMode draggedConfiguredMode;
    private OverlayDisplayMode resolvedAutoMode = OverlayDisplayMode.Expanded;
    private OverlayDisplayMode currentMode = OverlayDisplayMode.Expanded;
    private Rect lastClientBounds = Rect.Empty;
    private double lastTargetLeft = double.NaN;
    private double lastTargetTop = double.NaN;
    private bool placementDirty = true;
    private bool placementInProgress;
    private double modeAvailableWidth = double.PositiveInfinity;
    private string indicatorSignature = string.Empty;
    private double expandedLogicalWidth = 1;
    private double expandedMinimumWidth = 190;
    private double accountScrollOffset;
    private ScrollViewer? accountScroller;
    private double availableClientWidth = double.PositiveInfinity;

    public OverlayWindow(OverlaySettings settings, SafeLogger logger)
    {
        this.settings = settings;
        this.logger = logger;
        Icon = AppIcons.WindowIcon;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        ApplyScale();

        Content = shell;
        InitializeAccountReorder();
        usageHover.Closed += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (quotaRebuildPending && !usageHover.IsOpen && !isDragging && !dragPending && !accountReordering && accountHoldButton is null)
            {
                quotaRebuildPending = false;
                RebuildContent();
            }
        }));
        RebuildContent();
        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, this))
            {
                FinishDrag();
            }
        };
        SourceInitialized += OnSourceInitialized;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                FinishDrag();
                compactPopup.IsOpen = false;
                usageHover.Close();
            }
        };
    }

    public Action<string>? OnSwitchProfile { get; set; }
    public Action<IReadOnlyList<string>>? OnReorderProfiles { get; set; }

    public Action? OnRefreshProfiles { get; set; }
    public Func<string, Task>? OnRefreshQuota { get; set; }
    public Func<Task>? OnRefreshAllQuotas { get; set; }

    public Action? OnOpenProfilesFolder { get; set; }

    public Action? OnOpenApplicationDataFolder { get; set; }

    public Action? OnOpenSettings { get; set; }

    public Action? OnManageProfiles { get; set; }

    public Action? OnAddProfile { get; set; }

    public Action? OnHideOverlay { get; set; }

    public Action? OnExit { get; set; }

    public Action<OverlaySettings>? OnSettingsChanged { get; set; }

    public Localizer? Localizer { get; set; }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public bool AllowAutoShow { get; set; } = true;

    public void AttachTo(IntPtr codexHwnd)
    {
        if (ownerHwnd != codexHwnd)
        {
            FinishAccountReorder(commit: false);
            compactPopup.IsOpen = false;
            usageHover.Close();
            placementDirty = true;
        }
        ownerHwnd = codexHwnd;
        var helper = new WindowInteropHelper(this);
        _ = helper.EnsureHandle();
        helper.Owner = codexHwnd;
        ApplyToolWindowStyle(helper.Handle);
    }

    public void DetachFromOwner()
    {
        Hide();
        compactPopup.IsOpen = false;
        new WindowInteropHelper(this).Owner = IntPtr.Zero;
        ownerHwnd = IntPtr.Zero;
    }

    public void UpdatePlacement(IntPtr codexHwnd)
    {
        if (placementInProgress) return;
        placementInProgress = true;
        try { UpdatePlacementCore(codexHwnd); }
        finally { placementInProgress = false; }
    }

    private void UpdatePlacementCore(IntPtr codexHwnd)
    {
        if (!NativeMethods.IsWindowVisible(codexHwnd) || NativeMethods.IsIconic(codexHwnd))
        {
            Hide();
            compactPopup.IsOpen = false;
            return;
        }

        if (!TryGetClientBounds(codexHwnd, out Rect clientBounds))
        {
            Hide();
            compactPopup.IsOpen = false;
            return;
        }

        // The tracking timer must not reposition or rebuild the surface mid-drag.
        if (isDragging)
        {
            return;
        }

        var captionButtons = GetCaptionButtonsBounds(codexHwnd, clientBounds);
        if (!lastClientBounds.IsEmpty && (!NearlyEqual(clientBounds.Width, lastClientBounds.Width)
            || !NearlyEqual(clientBounds.Height, lastClientBounds.Height))) draggedLogicalWidth = null;
        ClearChangedDragWidthLock();
        var row = CalculateRowLayout(clientBounds, captionButtons,
            settings.DisplayMode == OverlayDisplayMode.Auto ? expandedLogicalWidth : null);
        modeAvailableWidth = row.AvailableWidth;
        availableClientWidth = row.AvailableWidth;
        OverlayDisplayMode nextMode = draggedLogicalWidth is not null ? currentMode
            : layoutService.ResolveDisplayMode(settings.DisplayMode, modeAvailableWidth, resolvedAutoMode, expandedLogicalWidth * SanitizedScale);
        if (settings.DisplayMode == OverlayDisplayMode.Auto)
        {
            resolvedAutoMode = nextMode;
        }

        if (nextMode != currentMode)
        {
            currentMode = nextMode;
            RebuildContent();
        }

        row = CalculateRowLayout(clientBounds, captionButtons);
        availableClientWidth = row.AvailableWidth;

        UpdateWidth();

        bool geometryChanged = placementDirty || !NearlyEqual(clientBounds, lastClientBounds);
        if (geometryChanged)
        {
            UpdateLayout();
        }

        var placement = new OverlayPlacement(row.OffsetX, row.OffsetY);

        // Clamping is temporary: a smaller owner (or a transient restore/resize)
        // must not replace the position chosen by dragging or in settings.
        // Keep the requested offsets so a larger owner restores that position.

        if (!double.IsFinite(placement.OffsetX) || !double.IsFinite(placement.OffsetY))
        {
            return;
        }
        if (!row.Fits || OverlayLayoutService.OverlapsCaption(placement, ActualWidth, ActualHeight, captionButtons))
        {
            Hide();
            compactPopup.IsOpen = false;
            return;
        }

        double targetLeft = clientBounds.Left + placement.OffsetX;
        double targetTop = clientBounds.Top + placement.OffsetY;
        bool positionChanged = geometryChanged
            || !NearlyEqual(targetLeft, lastTargetLeft)
            || !NearlyEqual(targetTop, lastTargetTop);
        if (positionChanged)
        {
            Left = targetLeft;
            Top = targetTop;
            lastTargetLeft = targetLeft;
            lastTargetTop = targetTop;
            lastClientBounds = clientBounds;
            placementDirty = false;
        }

        bool wasVisible = IsVisible;
        IntPtr handle = Handle;
        // Windows may hide/minimize an owned HWND while WPF still reports it
        // visible. Recover that native state when the controller permits display.
        bool nativeHidden = !NativeMethods.IsWindowVisible(handle) || NativeMethods.IsIconic(handle);
        if (AllowAutoShow && settings.ShowAutomaticallyWhenCodexOpens && NativeMethods.IsIconic(handle))
            WindowState = WindowState.Normal;
        if (!IsVisible && settings.ShowAutomaticallyWhenCodexOpens && AllowAutoShow)
        {
            Show();
        }

        if (IsVisible && AllowAutoShow && (positionChanged || !wasVisible || nativeHidden))
        {
            if (nativeHidden) NativeMethods.ShowWindow(handle, 4); // SW_SHOWNOACTIVATE
            Point devicePosition = hwndSource?.CompositionTarget?.TransformToDevice.Transform(new Point(Left, Top))
                ?? new Point(Left, Top);
            _ = NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                (int)Math.Round(devicePosition.X),
                (int)Math.Round(devicePosition.Y),
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        }
    }

    public void SetProfiles(IReadOnlyList<ProfileInfo> newProfiles, string? newActiveProfile)
    {
        bool unchanged = string.Equals(activeProfile, newActiveProfile, StringComparison.OrdinalIgnoreCase)
            && profiles.SequenceEqual(newProfiles);
        profiles = newProfiles;
        activeProfile = newActiveProfile;
        if (!unchanged)
        {
            indicatorSignature = BuildIndicatorSignature();
            RebuildContent();
        }
    }

    public void SetStatusDocument(ProfileStatusDocument document, string? recommendedProfileId)
    {
        statusDocument = document;
        recommendedProfile = recommendedProfileId;
        string newSignature = BuildIndicatorSignature();
        if (!string.Equals(indicatorSignature, newSignature, StringComparison.Ordinal))
        {
            indicatorSignature = newSignature;
            if (isDragging || dragPending || accountReordering || accountHoldButton is not null || usageHover.IsOpen)
            {
                quotaRebuildPending = true;
                // Update the open quota card without unloading its hovered button.
                usageHover.RefreshContent();
            }
            else
            {
                RebuildContent();
            }
        }
    }

    public void SetSwitching(bool switching)
    {
        if (switching) FinishAccountReorder(commit: false);
        isSwitching = switching;
        compactPopup.IsOpen = false;
        foreach (Button button in profileButtons)
        {
            button.IsEnabled = !switching;
        }
    }

    public void ShowNotification(string message) => ToastWindow.Show(ownerHwnd, message, isError: false);

    public void ShowError(string message) => ToastWindow.Show(ownerHwnd, message, isError: true);

    public void ApplySettings()
    {
        ApplyScale();
        RebuildContent();
    }

    private void RebuildContent()
    {
        if (accountScroller is not null) accountScrollOffset = accountScroller.HorizontalOffset;
        accountScroller = null;
        quotaRebuildPending = false;
        FinishAccountReorder(commit: false);
        usageHover.Close();
        // Quota refresh replaces the buttons. Close the native popup before detaching
        // its visual tree, then reopen against the persistent shell after layout.
        bool reopenPopup = compactPopup.IsOpen && currentMode == OverlayDisplayMode.Compact;
        compactPopup.IsOpen = false;
        placementDirty = true;
        if (isBallMinimized)
        {
            BuildFloatingBall();
            return;
        }
        shell.Height = HeaderHeight;
        shell.MinHeight = HeaderHeight;
        shell.Background = FindBrush("OverlayBackgroundBrush");
        shell.BorderBrush = FindBrush("OverlayBorderBrush");
        shell.BorderThickness = new Thickness(1);
        shell.CornerRadius = new CornerRadius(8);
        // Measure the complete row, including quota text, active marker, margins
        // and menu button. Keep this measurement even in compact mode so auto
        // can decide whether all accounts will fit when the owner grows.
        UIElement expandedContent = BuildExpandedContent();
        expandedContent.Measure(new System.Windows.Size(double.PositiveInfinity, HeaderButtonHeight));
        // Border/padding plus two DIPs for per-control device-pixel rounding.
        double measuredWidth = Math.Max(1, Math.Ceiling(expandedContent.DesiredSize.Width + 14));
        if (!NearlyEqual(measuredWidth, expandedLogicalWidth)) draggedLogicalWidth = null;
        expandedLogicalWidth = measuredWidth;
        ClearChangedDragWidthLock();
        expandedMinimumWidth = Math.Ceiling(((Grid)expandedContent).Children.OfType<FrameworkElement>()
            .Where(child => Grid.GetColumn(child) != 1).Sum(child => child.DesiredSize.Width) + 12 + 48);
        if (draggedLogicalWidth is null && settings.DisplayMode == OverlayDisplayMode.Auto && double.IsFinite(modeAvailableWidth))
        {
            currentMode = layoutService.ResolveDisplayMode(settings.DisplayMode, modeAvailableWidth, resolvedAutoMode, expandedLogicalWidth * SanitizedScale);
            resolvedAutoMode = currentMode;
        }
        shell.Padding = new Thickness(currentMode == OverlayDisplayMode.Compact ? 6 : 5, 1, currentMode == OverlayDisplayMode.Compact ? 6 : 5, 1);
        shell.Child = currentMode == OverlayDisplayMode.Compact ? BuildCompactContent() : expandedContent;
        if (currentMode == OverlayDisplayMode.Compact) accountScroller = null;
        UpdateWidth();
        compactPopup.Child = BuildCompactPopup();
        compactPopup.PlacementTarget = shell;
        if (reopenPopup && IsVisible)
        {
            UpdateLayout();
            if (ownerHwnd != IntPtr.Zero)
            {
                UpdatePlacement(ownerHwnd);
            }

            compactPopup.IsOpen = IsVisible && currentMode == OverlayDisplayMode.Compact;
        }
        if (ownerHwnd != IntPtr.Zero && !placementInProgress && !isDragging) UpdatePlacement(ownerHwnd);
    }

    private UIElement BuildCompactContent()
    {
        ProfileInfo? active = profiles.FirstOrDefault(profile => string.Equals(profile.Name, activeProfile, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault();

        var button = new Button
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            IsEnabled = !isSwitching,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Height = HeaderButtonHeight,
            MinHeight = 0,
        };
        if (active is not null && ShowQuota)
        {
            button.ToolTip = BuildUsageToolTip(active.Name);
            ConfigureUsageHover(button);
        }
        button.Click += (_, _) =>
        {
            // The shell survives content rebuilds; an account button does not.
            compactPopup.PlacementTarget = shell;
            compactPopup.IsOpen = true;
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = CreateAvatar(active?.Initials ?? "--", active?.Accent ?? "#A970FF");
        Grid.SetColumn(avatar, 0);
        grid.Children.Add(avatar);

        var name = new TextBlock
        {
            Text = isSwitching ? Localizer?["Switching"] ?? "Switching..." : active?.DisplayName ?? Localizer?["NoReadyProfiles"] ?? "No ready profiles",
            Foreground = FindBrush("StrongTextBrush"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var nameAndQuota = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameRow = AccountPlanBadgeBuilder.BesideName(name,
            active is null ? null : statusDocument?.Snapshots.GetValueOrDefault(active.Name)?.PlanName,
            Localizer?.Language ?? settings.Language, showPlan: settings.ShowAccountPlanBadges);
        nameRow.Margin = new Thickness(9, 0, 8, 0);
        nameAndQuota.Children.Add(nameRow);
        if (active is not null && ShowQuota)
        {
            nameAndQuota.Children.Add(CreateQuotaText(active.Name, new Thickness(9, 1, 8, 0)));
        }
        Grid.SetColumn(nameAndQuota, 1);
        grid.Children.Add(nameAndQuota);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        string activeIndicator = active is null ? string.Empty : GetProfileIndicator(active.Name);
        if (!ShowQuota && !string.IsNullOrEmpty(activeIndicator))
        {
            right.Children.Add(CreateIndicator(active!.Name, activeIndicator, new Thickness(0, 0, 8, 0)));
        }

        right.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = FindBrush("AccentBrush"), Margin = new Thickness(0, 0, 9, 0) });
        right.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 2 4 L 7 9 L 12 4"),
            Stroke = FindBrush("MutedTextBrush"),
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 14,
            Height = 12,
            Stretch = Stretch.Uniform,
        });
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        button.Content = grid;
        button.MouseEnter += (_, _) => shell.Background = FindBrush("TabHoverBrush");
        button.MouseLeave += (_, _) => shell.Background = FindBrush("OverlayBackgroundBrush");
        if (profiles.Count < 2) return button;
        var combined = new Grid();
        combined.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        combined.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        combined.Children.Add(CreateOverviewButton());
        Grid.SetColumn(button, 1);
        combined.Children.Add(button);
        return combined;
    }

    private UIElement BuildExpandedContent()
    {
        profileButtons.Clear();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (profiles.Count >= 2) grid.Children.Add(CreateOverviewButton());

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.RequestBringIntoView += (_, e) =>
        {
            // Pressing a profile focuses its button before the drag threshold.
            // Do not let that focus scroll the row while moving the overlay.
            if (dragPending || isDragging)
            {
                e.Handled = true;
            }
        };
        if (profiles.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = Localizer?["NoReadyProfiles"] ?? "No ready profiles",
                Foreground = FindBrush("MutedTextBrush"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 14, 0),
            });
        }

        foreach (ProfileInfo profile in profiles)
        {
            Button button = CreateProfileButton(profile, string.Equals(profile.Name, activeProfile, StringComparison.OrdinalIgnoreCase));
            panel.Children.Add(button);
            profileButtons.Add(button);
        }

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            ClipToBounds = true,
            Content = panel,
            Height = HeaderButtonHeight,
        };
        accountScroller = scrollViewer;
        double restoreOffset = accountScrollOffset;
        scrollViewer.Loaded += (_, _) => scrollViewer.ScrollToHorizontalOffset(restoreOffset);
        scrollViewer.ScrollChanged += (_, _) => accountScrollOffset = scrollViewer.HorizontalOffset;
        scrollViewer.PreviewMouseWheel += (_, args) =>
        {
            if (accountReordering || dragPending || isDragging) return;
            // Wheel motion scrolls just the central viewport, including over
            // quota text and avatars. Hidden means no scrollbar is reserved.
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - args.Delta * 0.75);
            args.Handled = true;
        };
        Grid.SetColumn(scrollViewer, 1);

        Button menuButton = CreateMenuButton();
        Grid.SetColumn(menuButton, 2);
        grid.Children.Add(scrollViewer);
        grid.Children.Add(menuButton);
        return grid;
    }

    private UIElement BuildCompactPopup()
    {
        var border = new Border
        {
            Width = 370,
            Background = FindBrush("OverlayBackgroundBrush"),
            BorderBrush = FindBrush("OverlayBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
        };

        var panel = new StackPanel();
        foreach (ProfileInfo profile in profiles)
        {
            bool isActive = string.Equals(profile.Name, activeProfile, StringComparison.OrdinalIgnoreCase);
            string indicator = GetProfileIndicator(profile.Name);
            Button item = CreateProfilePopupButton(profile, indicator, isActive);
            if (ShowQuota || !string.IsNullOrEmpty(indicator))
            {
                item.ToolTip = BuildUsageToolTip(profile.Name);
                ConfigureUsageHover(item);
            }
            item.IsEnabled = !isActive && !isSwitching;
            string name = profile.Name;
            item.Click += (_, _) =>
            {
                compactPopup.IsOpen = false;
                OnSwitchProfile?.Invoke(name);
            };
            panel.Children.Add(item);
        }

        panel.Children.Add(new Separator { Margin = new Thickness(2, 5, 2, 5) });
        panel.Children.Add(CreatePopupCommand(Localizer?["AddProfile"] ?? "Add profile", OnManageProfiles));
        panel.Children.Add(CreatePopupCommand(Localizer?["Settings"] ?? "Settings", OnOpenSettings));
        border.Child = panel;
        return border;
    }

    private Button CreateOverviewButton()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M0,0 H6 V6 H0 Z M9,0 H15 V6 H9 Z M0,9 H6 V15 H0 Z M9,9 H15 V15 H9 Z"),
            Fill = FindBrush("AccentBrush"), Width = 15, Height = 15, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 7, 0) });
        row.Children.Add(new TextBlock { Text = Localizer?["Overview"] ?? "Overview", FontSize = 12, Foreground = FindBrush("StrongTextBrush"), VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = row, Height = HeaderButtonHeight, MinHeight = 0, Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 0), Background = FindBrush("TabBackgroundBrush"), BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = AccountsOverviewBuilder.Build(profiles, activeProfile,
                name => statusDocument?.Snapshots.GetValueOrDefault(name), Localizer?.Language ?? settings.Language, OnRefreshAllQuotas, settings.ShowAccountPlanBadges) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "AccountsOverview");
        usageHover.Attach(button, (Popup)button.ToolTip, shell, openOnClick: true);
        button.MouseEnter += (_, _) => AnimateBrush(button, "TabHoverBrush");
        button.MouseLeave += (_, _) => AnimateBrush(button, "TabBackgroundBrush");
        return button;
    }

    private Button CreateProfileButton(ProfileInfo profile, bool isActive)
    {
        var avatar = CreateAvatar(profile.Initials, profile.Accent, 22);
        avatar.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
        avatar.BorderBrush = isActive ? FindBrush("StrongTextBrush") : Brushes.Transparent;

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(avatar);
        var nameAndQuota = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = profile.DisplayName,
            FontSize = 13.5,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = isActive ? FindBrush("StrongTextBrush") : FindBrush("MutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 112,
        };
        var nameRow = AccountPlanBadgeBuilder.BesideName(name,
            statusDocument?.Snapshots.GetValueOrDefault(profile.Name)?.PlanName,
            Localizer?.Language ?? settings.Language, showPlan: settings.ShowAccountPlanBadges);
        nameRow.Margin = new Thickness(8, 0, 0, 0);
        nameAndQuota.Children.Add(nameRow);
        if (ShowQuota)
        {
            nameAndQuota.Children.Add(CreateQuotaText(profile.Name, new Thickness(8, 2, 0, 0)));
        }
        row.Children.Add(nameAndQuota);

        string indicator = GetProfileIndicator(profile.Name);
        if (!ShowQuota && !string.IsNullOrEmpty(indicator))
        {
            row.Children.Add(CreateIndicator(profile.Name, indicator, new Thickness(4, 0, 0, 0)));
        }

        var content = new Grid();
        if (isActive)
        {
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(new Border
            {
                Width = 3,
                Height = 20,
                Background = FindBrush("AccentBrush"),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(row, 1);
        }

        content.Children.Add(row);

        var button = new Button
        {
            Content = content,
            MinWidth = 108,
            MaxWidth = ShowQuota ? double.PositiveInfinity : 300,
            Height = HeaderButtonHeight,
            MinHeight = 0,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = isActive ? new Thickness(7, 0, 10, 0) : new Thickness(9, 0, 9, 0),
            BorderThickness = isActive ? new Thickness(1) : new Thickness(0),
            BorderBrush = isActive ? FindBrush("AccentHoverBrush") : Brushes.Transparent,
            Background = isActive ? FindBrush("TabActiveBrush") : FindBrush("TabBackgroundBrush"),
            Cursor = isActive ? Cursors.Arrow : Cursors.Hand,
            Tag = profile.Name,
            ToolTip = ShowQuota ? BuildUsageToolTip(profile.Name) : profile.DisplayName,
            IsEnabled = !isSwitching,
        };

        ConfigureUsageHover(button);
        button.Click += (_, _) =>
        {
            if (accountReordering) return;
            if (!isActive)
            {
                OnSwitchProfile?.Invoke(profile.Name);
            }
        };
        button.MouseEnter += (_, _) => AnimateBrush(button, isActive ? "TabActiveBrush" : "TabHoverBrush");
        button.MouseLeave += (_, _) => AnimateBrush(button, isActive ? "TabActiveBrush" : "TabBackgroundBrush");
        return button;
    }

    private string GetProfileIndicator(string profileId)
    {
        if (statusDocument is null)
        {
            return ShowQuota ? Localizer?["QuotaUnknown"] ?? "Quota unavailable" : string.Empty;
        }

        if (settings.ShowAutomaticLimitIndicators && settings.ShowIndicatorsInOverlay)
        {
            UsageSnapshot? snapshot = statusDocument.Snapshots.TryGetValue(profileId, out UsageSnapshot? snap) ? snap : null;
            return UsageQuotaFormatter.Summary(snapshot, Localizer?.Language ?? LanguagePreference.English,
                DateTimeOffset.UtcNow, TimeSpan.FromMinutes(settings.StaleDataThresholdMinutes));
        }

        if (!settings.ShowManualProfileEmojiInOverlay)
        {
            return string.Empty;
        }

        string? manual = statusDocument.Profiles
            .FirstOrDefault(status => profileId.Equals(status.ProfileId, StringComparison.OrdinalIgnoreCase))
            ?.ManualEmoji;
        return FirstTextElement(manual);
    }

    private FrameworkElement CreateIndicator(string profileId, string indicator, Thickness margin)
    {
        FrameworkElement icon = CreateIndicatorVisual(indicator, 13);
        icon.Margin = margin;
        icon.ToolTip = BuildUsageToolTip(profileId);
        ConfigureUsageHover(icon);
        return icon;
    }

    private Popup BuildUsageToolTip(string profileId)
    {
        var language = Localizer?.Language ?? settings.Language;
        var popup = UsageToolTipBuilder.Build(statusDocument?.Snapshots.GetValueOrDefault(profileId), language);
        var surface = (Border)popup.Child;
        var refreshState = new QuotaRefreshButtonBuilder.State();
        void Refresh()
        {
            var updated = UsageToolTipBuilder.Build(statusDocument?.Snapshots.GetValueOrDefault(profileId), language);
            var updatedSurface = (Border)updated.Child;
            var card = (Border)updatedSurface.Child;
            updatedSurface.Child = null;
            var rows = (StackPanel)card.Child;
            var refresh = QuotaRefreshButtonBuilder.Build("RefreshQuota", language,
                OnRefreshQuota is null ? null : () => OnRefreshQuota(profileId), Refresh, refreshState);
            refresh.Margin = new Thickness(0, 12, 0, 0);
            rows.Children.Add(refresh);
            surface.Child = card;
        }
        popup.Tag = (Action)Refresh;
        var countdown = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        countdown.Tick += (_, _) => Refresh();
        popup.Opened += (_, _) => { Refresh(); countdown.Start(); };
        popup.Closed += (_, _) => countdown.Stop();
        Refresh();
        return popup;
    }

    private void ConfigureUsageHover(FrameworkElement target)
    {
        if (target.ToolTip is Popup tooltip)
        {
            usageHover.Attach(target, tooltip);
        }
    }

    private string BuildIndicatorSignature()
        => string.Join('|', profiles.Select(profile => profile.Name + ":" + GetProfileIndicator(profile.Name)
            + ":" + (statusDocument?.Snapshots.GetValueOrDefault(profile.Name)?.CapturedAt.ToString("O") ?? string.Empty)
            + ":" + (statusDocument?.Snapshots.GetValueOrDefault(profile.Name)?.PlanName ?? string.Empty)));

    private bool ShowQuota => settings.ShowAutomaticLimitIndicators && settings.ShowIndicatorsInOverlay;

    private TextBlock CreateQuotaText(string profileId, Thickness margin) => new()
    {
        Text = GetProfileIndicator(profileId),
        Foreground = FindBrush("MutedTextBrush"),
        FontSize = 11.5,
        Margin = margin,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private static string FirstTextElement(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value.Trim());
        return enumerator.MoveNext() ? enumerator.GetTextElement() : string.Empty;
    }

    private static bool NearlyEqual(Rect left, Rect right)
        => NearlyEqual(left.Left, right.Left)
            && NearlyEqual(left.Top, right.Top)
            && NearlyEqual(left.Width, right.Width)
            && NearlyEqual(left.Height, right.Height);

    private static bool NearlyEqual(double left, double right)
        => double.IsFinite(left) && double.IsFinite(right) && Math.Abs(left - right) < 0.25;

    private Button CreateMenuButton()
    {
        var plus = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 8 2 L 8 14 M 2 8 L 14 8"),
            Stroke = FindBrush("StrongTextBrush"),
            StrokeThickness = 2.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 16,
            Height = 16,
            Stretch = Stretch.None,
            SnapsToDevicePixels = true,
        };

        var button = new Button
        {
            Content = plus,
            Width = 34,
            Height = 32,
            MinHeight = 0,
            BorderThickness = new Thickness(0),
            Background = FindBrush("TabBackgroundBrush"),
            Cursor = Cursors.Hand,
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = Localizer?["AddProfile"] ?? "Add profile",
        };

        var menu = new ContextMenu
        {
            Background = FindBrush("Surface2Brush"),
            BorderBrush = FindBrush("BorderBrush"),
            Foreground = FindBrush("StrongTextBrush"),
            Padding = new Thickness(5),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        menu.Items.Add(CreateMenuItem(Localizer?["AddProfile"] ?? "Add profile", () => OnManageProfiles?.Invoke()));
        menu.Items.Add(CreateMenuItem(Localizer?["MinimizeToBall"] ?? "Minimize to ball", MinimizeToBall));
        menu.Items.Add(CreateMenuItem(Localizer?["Settings"] ?? "Settings", () => OnOpenSettings?.Invoke()));
        button.ContextMenu = menu;
        button.Click += (_, _) =>
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        };
        button.MouseEnter += (_, _) => AnimateBrush(button, "TabHoverBrush");
        button.MouseLeave += (_, _) => AnimateBrush(button, "TabBackgroundBrush");
        return button;
    }

    private Button CreateProfilePopupButton(ProfileInfo profile, string indicator, bool isActive)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (isActive)
        {
            grid.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2 7 L 6 11 L 14 3"),
                Stroke = FindBrush("AccentBrush"),
                StrokeThickness = 1.8,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var label = new TextBlock
        {
            Text = profile.DisplayName,
            Foreground = FindBrush("StrongTextBrush"),
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var nameAndQuota = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameAndQuota.Children.Add(AccountPlanBadgeBuilder.BesideName(label,
            statusDocument?.Snapshots.GetValueOrDefault(profile.Name)?.PlanName,
            Localizer?.Language ?? settings.Language, showPlan: settings.ShowAccountPlanBadges));
        if (ShowQuota)
        {
            nameAndQuota.Children.Add(CreateQuotaText(profile.Name, new Thickness(0, 3, 0, 0)));
        }
        Grid.SetColumn(nameAndQuota, 1);
        grid.Children.Add(nameAndQuota);

        if (!ShowQuota && !string.IsNullOrEmpty(indicator))
        {
            FrameworkElement icon = CreateIndicatorVisual(indicator, 13);
            icon.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(icon, 2);
            grid.Children.Add(icon);
        }

        return new Button
        {
            Content = grid,
            Height = ShowQuota ? 58 : 38,
            MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = FindBrush("Surface2Brush"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
    }

    private static FrameworkElement CreateIndicatorVisual(string indicator, double size)
    {
        if (indicator == "⭐")
        {
            return new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M 12 2 L 14.9 8.7 L 22 9.3 L 16.6 13.9 L 18.2 21 L 12 17.3 L 5.8 21 L 7.4 13.9 L 2 9.3 L 9.1 8.7 Z"),
                    Fill = new SolidColorBrush(Color.FromRgb(255, 213, 74)),
                    Stroke = new SolidColorBrush(Color.FromRgb(245, 178, 18)),
                    StrokeThickness = 1.2,
                    StrokeLineJoin = PenLineJoin.Round,
                },
            };
        }

        Color? color = indicator switch
        {
            "🟢" => Color.FromRgb(34, 197, 94),
            "🟡" => Color.FromRgb(250, 204, 21),
            "🔴" => Color.FromRgb(239, 68, 68),
            _ => null,
        };

        if (color is not null)
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color.Value),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 0.8,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true,
            };
        }

        return new TextBlock
        {
            Text = indicator,
            FontFamily = EmojiFont,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private Button CreatePopupCommand(string text, Action? action, bool closeOnInvoke = true)
    {
        Button button = CreatePopupButton(text, null);
        button.Click += (_, _) =>
        {
            if (closeOnInvoke)
            {
                compactPopup.IsOpen = false;
            }
            action?.Invoke();
        };
        return button;
    }

    private Button CreatePopupButton(string text, string? iconGeometry)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (iconGeometry is not null)
        {
            grid.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(iconGeometry),
                Stroke = FindBrush("AccentBrush"),
                StrokeThickness = 1.8,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var label = new TextBlock
        {
            Text = text,
            Foreground = FindBrush("StrongTextBrush"),
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        return new Button
        {
            Content = grid,
            Height = 38,
            MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = FindBrush("Surface2Brush"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private Border CreateAvatar(string initials, string accent, double size = 24)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(accent)!;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = brush,
            Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = size <= 20 ? 9 : 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        hwndSource = HwndSource.FromHwnd(handle);
        ApplyToolWindowStyle(handle);
    }

    private static void ApplyToolWindowStyle(IntPtr handle)
    {
        long exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        exStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
        exStyle &= ~NativeMethods.WsExAppWindow;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)exStyle);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TrackAccountHold(e.OriginalSource as DependencyObject, e.GetPosition(this))) return;
        if (ownerHwnd == IntPtr.Zero || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // Let buttons handle a normal click; capture only after the drag threshold.
        dragPending = true;
        dragOffset = e.GetPosition(this);
        dragStartScreen = DeviceToDip(PointToScreen(dragOffset));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (accountReordering)
        {
            if (e.LeftButton == MouseButtonState.Pressed) UpdateAccountReorder(e.GetPosition(this));
            else FinishAccountReorder(commit: false);
            e.Handled = true;
            return;
        }
        if (accountHoldButton is not null)
        {
            UpdateAccountHold(e.GetPosition(this));
            e.Handled = true;
            return;
        }
        if (!dragPending && !isDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || ownerHwnd == IntPtr.Zero || !TryGetClientBounds(ownerHwnd, out Rect clientBounds))
        {
            FinishDrag();
            return;
        }

        Point screenPoint = PointToScreen(e.GetPosition(this));
        Point screenDip = DeviceToDip(screenPoint);
        if (!isDragging)
        {
            if (Math.Abs(screenDip.X - dragStartScreen.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(screenDip.Y - dragStartScreen.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (!CaptureMouse())
            {
                dragPending = false;
                return;
            }

            BeginHeaderDrag();
        }

        var captionButtons = GetCaptionButtonsBounds(ownerHwnd, clientBounds);
        MoveHeaderDrag(clientBounds, new Point(screenDip.X - clientBounds.Left - dragOffset.X,
            screenDip.Y - clientBounds.Top - dragOffset.Y), captionButtons);
        e.Handled = true;
    }

    private void BeginHeaderDrag()
    {
        dragPending = false;
        isDragging = true;
        dragFixedWidth = ActualWidth;
        if (!isBallMinimized)
        {
            settings.PositionPreset = PositionPreset.Custom;
            draggedLogicalWidth = dragFixedWidth / SanitizedScale;
            draggedScale = SanitizedScale;
            draggedConfiguredMode = settings.DisplayMode;
        }
        compactPopup.IsOpen = false;
    }

    private void MoveHeaderDrag(Rect clientBounds, Point requestedOffset, OverlayCaptionBounds? captionButtons)
    {
        var row = layoutService.ConstrainFixedRow(clientBounds.Width, clientBounds.Height,
            dragFixedWidth, ActualHeight, requestedOffset.X, requestedOffset.Y, captionButtons);
        if (!isBallMinimized && IsRestrictedDragRow(Top - clientBounds.Top, ActualHeight, captionButtons)
            && !IsRestrictedDragRow(row.OffsetY, ActualHeight, captionButtons))
        {
            // Leaving the title row removes its width restriction. Show all
            // accounts again, then keep that width during ordinary movement.
            accountScrollOffset = 0;
            accountScroller?.ScrollToHorizontalOffset(0);
            if (currentMode != OverlayDisplayMode.Expanded)
            {
                currentMode = OverlayDisplayMode.Expanded;
                RebuildContent();
            }
            row = layoutService.ConstrainRow(PositionPreset.Custom, clientBounds.Width, clientBounds.Height,
                expandedLogicalWidth * SanitizedScale, ActualHeight, requestedOffset.X, requestedOffset.Y,
                captionButtons, expandedMinimumWidth * SanitizedScale);
            if (row.Fits) ApplyDragRowWidth(row);
        }
        if (!row.Fits && !isBallMinimized && Top - clientBounds.Top >= OverlayLayoutService.ProtectedTopRowHeight
            && row.OffsetY < OverlayLayoutService.ProtectedTopRowHeight)
        {
            // A wider undocked bar must be able to return to the title row.
            // Fit once on entry, then lock that width for subsequent movement.
            row = layoutService.ConstrainRow(PositionPreset.Custom, clientBounds.Width, clientBounds.Height,
                dragFixedWidth, ActualHeight, requestedOffset.X, requestedOffset.Y, captionButtons,
                MinimumRowWidth * SanitizedScale);
            if (row.Fits)
            {
                ApplyDragRowWidth(row);
            }
        }
        if (!row.Fits) return;
        var offset = new Point(row.OffsetX, row.OffsetY);
        if (isBallMinimized) ballOffset = offset;
        else { settings.OffsetX = offset.X; settings.OffsetY = offset.Y; }
        Left = clientBounds.Left + offset.X;
        Top = clientBounds.Top + offset.Y;
        placementDirty = true;
    }

    private void ApplyDragRowWidth(OverlayRowLayout row)
    {
        dragFixedWidth = row.Width;
        draggedLogicalWidth = row.Width / SanitizedScale;
        availableClientWidth = row.AvailableWidth;
        UpdateWidth();
        UpdateLayout();
    }

    private static bool IsRestrictedDragRow(double y, double height, OverlayCaptionBounds? captionButtons)
        => y < OverlayLayoutService.ProtectedTopRowHeight
            || (captionButtons is { IsValid: true }
                && y < captionButtons.Top + captionButtons.Height + OverlayLayoutService.CaptionSafetyGap
                && y + height > captionButtons.Top - OverlayLayoutService.CaptionSafetyGap);

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (accountReordering)
        {
            UpdateAccountReorder(e.GetPosition(this));
            FinishAccountReorder(commit: true);
            e.Handled = true;
            return;
        }
        if (accountHoldButton is not null)
        {
            bool moved = accountPressMoved;
            FinishDrag();
            if (moved) e.Handled = true;
            return;
        }
        bool wasDragging = isDragging;
        bool ballClick = isBallMinimized && dragPending && !wasDragging;
        FinishDrag();
        if (ballClick) { RestoreFromBall(); e.Handled = true; return; }
        if (wasDragging)
        {
            // Prevent releasing a drag from also opening a menu or switching accounts.
            e.Handled = true;
        }
    }

    private void FinishDrag()
    {
        FinishAccountReorder(commit: false);
        dragPending = false;
        bool wasDragging = isDragging;
        isDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (wasDragging && !isBallMinimized)
        {
            TrySaveSettings();
        }
        if (quotaRebuildPending)
        {
            quotaRebuildPending = false;
            RebuildContent();
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            OnSettingsChanged?.Invoke(settings);
        }
        catch (Exception exception)
        {
            logger.Error("Failed to save overlay settings.", exception);
        }
    }

    private OverlayCaptionBounds? GetCaptionButtonsBounds(IntPtr hwnd, Rect clientBounds)
    {
        if (NativeMethods.GetWindowRect(hwnd, out NativeRect windowRect))
        {
            Point origin = DeviceToDip(new Point(windowRect.Left, windowRect.Top));
            // DWM reports window-relative device pixels. Custom Electron frames
            // may return a zero-width rectangle, so keep a conservative fallback.
            if (NativeMethods.DwmGetCaptionButtonBounds(hwnd, NativeMethods.DwmwaCaptionButtonBounds,
                out NativeRect nativeButtons, System.Runtime.InteropServices.Marshal.SizeOf<NativeRect>()) == 0
                && nativeButtons.Width > 0 && nativeButtons.Height > 0)
            {
                Point start = DeviceToDip(new Point(windowRect.Left + nativeButtons.Left, windowRect.Top + nativeButtons.Top));
                Point size = DeviceToDip(new Point(nativeButtons.Width, nativeButtons.Height), treatAsSize: true);
                var rect = Rect.Intersect(clientBounds, new Rect(start.X, start.Y, size.X, size.Y));
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return null;
                return new OverlayCaptionBounds(rect.Left - clientBounds.Left, rect.Top - clientBounds.Top, rect.Width, rect.Height);
            }
            // Ordinary native title bars lie outside the client area.
            if (clientBounds.Top - origin.Y > 12) return null;
        }
        return new OverlayCaptionBounds(Math.Max(0, clientBounds.Width - 160), 0,
            Math.Min(160, clientBounds.Width), Math.Min(48, clientBounds.Height));
    }

    private bool TryGetClientBounds(IntPtr hwnd, out Rect bounds)
    {
        bounds = Rect.Empty;
        if (!NativeMethods.GetClientRect(hwnd, out NativeRect clientRect))
        {
            return false;
        }

        var topLeft = new NativePoint { X = 0, Y = 0 };
        if (!NativeMethods.ClientToScreen(hwnd, ref topLeft))
        {
            return false;
        }

        Point origin = DeviceToDip(new Point(topLeft.X, topLeft.Y));
        Point size = DeviceToDip(new Point(clientRect.Width, clientRect.Height), treatAsSize: true);
        bounds = new Rect(origin.X, origin.Y, size.X, size.Y);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private Point DeviceToDip(Point point, bool treatAsSize = false)
    {
        if (hwndSource?.CompositionTarget is null)
        {
            return point;
        }

        Matrix transform = hwndSource.CompositionTarget.TransformFromDevice;
        return treatAsSize
            ? new Point(point.X * transform.M11, point.Y * transform.M22)
            : transform.Transform(point);
    }

    private SolidColorBrush FindBrush(string resourceKey)
    {
        return ((SolidColorBrush)Application.Current.FindResource(resourceKey)).Clone();
    }

    private double SanitizedScale => double.IsFinite(settings.Scale) ? Math.Clamp(settings.Scale, 0.8, 1.4) : 1;

    private double LogicalWidth => Math.Min(draggedLogicalWidth ?? double.PositiveInfinity,
        currentMode == OverlayDisplayMode.Compact ? (ShowQuota ? 350 : 286) : expandedLogicalWidth);
    private void ClearChangedDragWidthLock()
    {
        if (draggedLogicalWidth is not null && (!NearlyEqual(draggedScale, SanitizedScale)
            || draggedConfiguredMode != settings.DisplayMode)) draggedLogicalWidth = null;
    }
    private double MinimumRowWidth => isBallMinimized ? 32 : currentMode == OverlayDisplayMode.Expanded ? expandedMinimumWidth : profiles.Count >= 2 ? 190 : 110;

    private OverlayRowLayout CalculateRowLayout(Rect clientBounds, OverlayCaptionBounds? captionButtons, double? desiredWidth = null)
        => layoutService.ConstrainRow(isBallMinimized ? PositionPreset.Custom : settings.PositionPreset,
            clientBounds.Width, clientBounds.Height, (isBallMinimized ? 32 : desiredWidth ?? LogicalWidth) * SanitizedScale,
            (isBallMinimized ? 32 : HeaderHeight) * SanitizedScale,
            isBallMinimized ? ballOffset.X : settings.OffsetX, isBallMinimized ? ballOffset.Y : settings.OffsetY,
            captionButtons, MinimumRowWidth * SanitizedScale);

    private void UpdateWidth()
    {
        double targetWidth = Math.Max(1, Math.Min((isBallMinimized ? 32 : LogicalWidth) * SanitizedScale, availableClientWidth));
        if (!NearlyEqual(Width, targetWidth))
        {
            Width = targetWidth;
            placementDirty = true;
        }
    }

    private void ApplyScale()
    {
        double scale = SanitizedScale;
        shell.LayoutTransform = Math.Abs(scale - 1) < 0.001
            ? Transform.Identity
            : new ScaleTransform(scale, scale);
    }

    private void AnimateBrush(Control control, string resourceKey)
    {
        if (!settings.AnimationsEnabled || Application.Current.FindResource(resourceKey) is not SolidColorBrush target)
        {
            return;
        }

        if (control.Background is not SolidColorBrush current)
        {
            current = new SolidColorBrush(Colors.Transparent);
            control.Background = current;
        }

        current.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target.Color, AnimationDuration));
    }
}
