using TodoVoiceMaui.Views;

namespace TodoVoiceMaui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(TodoDetailPage), typeof(TodoDetailPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
    }
}