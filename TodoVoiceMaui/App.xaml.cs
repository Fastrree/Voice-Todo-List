using TodoVoiceMaui.Views;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui;

public partial class App : Application
{
	private readonly SyncService _syncService;
	private readonly ReminderService _reminderService;

	public App(SyncService syncService, ReminderService reminderService)
	{
		InitializeComponent();
		_syncService = syncService;
		_reminderService = reminderService;
		ThemeService.ApplySavedTheme();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new ContentPage
		{
			Content = new Label
			{
				Text = "Yükleniyor...",
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			}
		});

		_ = InitializeAsync(window);

		return window;
	}

	private async Task InitializeAsync(Window window)
	{
		try
		{
			await _syncService.InitializeAsync();

			// Prototype flow: no login required, go straight to the todo list.
			var isLoggedIn = await _syncService.IsUserLoggedInAsync();
			if (isLoggedIn)
			{
				_reminderService.Start();
			}

			window.Page = new AppShell();
		}
		catch (Exception ex)
		{
			Log("Init error: " + ex.ToString());
			window.Page = new ContentPage
			{
				Content = new Label
				{
					Text = "Hata: " + ex.Message,
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center
				}
			};
		}
	}

	private void Log(string message)
	{
		try
		{
			System.IO.File.AppendAllText(
				System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
				DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
		}
		catch { }
	}

	protected override void OnSleep()
	{
		// Handle when your app sleeps
	}

	protected override void OnResume()
	{
		// Handle when your app resumes
	}
}
