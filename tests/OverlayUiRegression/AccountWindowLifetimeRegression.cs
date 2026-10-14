using System.Reflection;
using System.Windows;

internal static partial class Program
{
    private static void RunAccountWindowLifetimeScenario()
    {
        var method = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.CodexWindowFinder")!
            .GetMethod("HasOpenWindow", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Open() => (bool)method.Invoke(null, [Environment.ProcessId])!;
        var first = new Window { Title = "Account lifecycle fixture 1", Width = 400, Height = 200 };
        var second = new Window { Title = "Account lifecycle fixture 2", Width = 400, Height = 200 };
        try
        {
            first.Show(); second.Show(); Pump();
            Require(Open(), "Visible account windows must prevent background cleanup.");
            first.Hide(); Pump();
            Require(Open(), "Another window in the same account desktop must keep its background alive.");
            second.WindowState = WindowState.Minimized; Pump();
            Require(Open(), "Minimizing the last window must not be mistaken for closing it.");
            second.Hide(); Pump();
            Require(!Open(), "Hiding a minimized window to the tray must be treated as closed, not minimized.");
            second.WindowState = WindowState.Normal;
            second.Show(); Pump();
            Require(Open(), "Reopening a hidden account window must cancel cleanup.");
            first.Close(); second.Close(); Pump();
            Require(!Open(), "Destroyed account windows must not leave their background marked open.");
            Evidence.Add(new { scenario = "account-window-lifetime", nativeChecks = 6 });
        }
        finally { first.Close(); second.Close(); Pump(); }
    }
}
