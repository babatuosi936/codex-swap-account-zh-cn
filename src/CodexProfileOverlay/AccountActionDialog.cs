using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexProfileOverlay;

internal enum AccountAction
{
    Cancel,
    OpenIndependentWindow,
    SwitchCurrentAccount,
}

/// <summary>Lets the user choose whether an account opens in another window or replaces the current sign-in.</summary>
internal sealed class AccountActionDialog : Window
{
    private AccountAction choice;

    private AccountActionDialog(
        string title,
        string message,
        string openText,
        string switchText,
        string cancelText,
        bool preferIndependentWindow)
    {
        Title = title;
        Icon = AppIcons.WindowIcon;
        Width = 540;
        Height = 258;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Foreground = (Brush)Application.Current.FindResource("StrongTextBrush");
        ShowInTaskbar = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var chrome = new Border
        {
            Background = (Brush)Application.Current.FindResource("OverlayBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("OverlayBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
        };
        WindowDragHelper.Enable(this, chrome);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        chrome.Child = root;

        var heading = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
        };
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var description = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("MutedTextBrush"),
            FontSize = 14,
            LineHeight = 21,
        };
        Grid.SetRow(description, 2);
        root.Children.Add(description);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        actions.Children.Add(CreateButton(openText, AccountAction.OpenIndependentWindow, true, preferIndependentWindow));
        actions.Children.Add(CreateButton(switchText, AccountAction.SwitchCurrentAccount, false, !preferIndependentWindow));
        actions.Children.Add(new Button
        {
            Content = cancelText,
            MinWidth = 88,
            MinHeight = 36,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
        });
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = chrome;
        Loaded += (_, _) => Activate();
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key != Key.Escape) return;
            choice = AccountAction.Cancel;
            DialogResult = false;
            Close();
        };
    }

    private Button CreateButton(string text, AccountAction action, bool primary, bool isDefault)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = primary ? 150 : 128,
            MinHeight = 36,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
        };
        if (primary)
        {
            button.Style = (Style)Application.Current.FindResource("PrimaryButtonStyle");
        }

        button.Click += (_, _) =>
        {
            choice = action;
            DialogResult = true;
            Close();
        };
        return button;
    }

    public static AccountAction Show(
        Window? owner,
        IntPtr nativeOwner,
        string title,
        string message,
        string openText,
        string switchText,
        string cancelText,
        bool preferIndependentWindow)
    {
        var dialog = new AccountActionDialog(title, message, openText, switchText, cancelText, preferIndependentWindow);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }
        else if (nativeOwner != IntPtr.Zero)
        {
            new WindowInteropHelper(dialog).Owner = nativeOwner;
        }

        if (owner is { IsVisible: true })
        {
            DialogPlacement.BelowAnchor(dialog, owner);
        }
        else
        {
            DialogPlacement.CenterOnOwnerScreen(dialog, owner, nativeOwner);
        }
        return dialog.ShowDialog() == true ? dialog.choice : AccountAction.Cancel;
    }
}
