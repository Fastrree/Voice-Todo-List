using Microsoft.Extensions.Logging;
using TodoVoiceMaui.ViewModels;
using TodoVoiceMaui.Views;
using TodoVoiceMaui.Services;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;

namespace TodoVoiceMaui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
			});

		// Register services
		builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
		builder.Services.AddHttpClient();
		builder.Services.AddSingleton<SupabaseService>();
		builder.Services.AddSingleton<AudioService>();
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<SyncService>();
		builder.Services.AddSingleton<ReminderService>();
		builder.Services.AddSingleton<SpeechToTextService>();

		// Register ViewModels
		builder.Services.AddSingleton<MainPageViewModel>();
		builder.Services.AddTransient<LoginPageViewModel>();
		builder.Services.AddTransient<TodoListPageViewModel>();
		builder.Services.AddTransient<TodoDetailPageViewModel>();
		builder.Services.AddTransient<SettingsPageViewModel>();

		// Register Views
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<TodoListPage>();
		builder.Services.AddTransient<TodoDetailPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}