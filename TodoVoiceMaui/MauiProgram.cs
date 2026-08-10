using Microsoft.Extensions.Logging;
using TodoVoiceMaui.ViewModels;
using TodoVoiceMaui.Views;
using TodoVoiceMaui.Services;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;
using TodoVoiceMaui.Core.Application.Voice;
using TodoVoiceMaui.Core.Application.Todos;

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
				fonts.AddFont("Sora-Regular.ttf", "Sora");
				fonts.AddFont("Sora-Medium.ttf", "SoraMedium");
				fonts.AddFont("Sora-SemiBold.ttf", "SoraSemiBold");
				fonts.AddFont("Sora-Bold.ttf", "SoraBold");
			});

		// Register services
		builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
		builder.Services.AddHttpClient();
		builder.Services.AddSingleton<SupabaseService>();
		builder.Services.AddSingleton<AudioService>();
		builder.Services.AddSingleton<ITodoStore, DatabaseService>();
		builder.Services.AddSingleton<SyncService>();
		builder.Services.AddSingleton<ReminderService>();
		builder.Services.AddSingleton<SpeechToTextService>();

		// Voice core wiring
		builder.Services.AddSingleton<IVoiceCommandParser, RuleBasedVoiceCommandParser>();
		builder.Services.AddSingleton<ITodoCommandSink, TodoCommandSink>();
		builder.Services.AddSingleton<IVoiceCommandHandler, TodoVoiceCommandHandler>();

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