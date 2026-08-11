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

		// Arayüz ses efektleri tercihi (Ayarlar → Ses efektleri)
		SoundEffectService.Enabled = Preferences.Default.Get("enable_sounds", true);

		// Dev aracı: `--theme=dark|light|system` ile tema zorlanabilir (tema doğrulaması için).
		// Argüman yoksa kayıtlı tercih uygulanır.
		var args = Environment.GetCommandLineArgs();
		var themeArg = args.FirstOrDefault(a => a.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
		if (themeArg != null)
		{
			ThemeService.ApplyTheme(themeArg.Substring("--theme=".Length));
		}
		else
		{
			ThemeService.ApplySavedTheme();
		}
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

		// Liquid Glass KATMAN 0: pencere seviyesinde Mica/Acrylic backdrop.
		// Handler hazır olduğunda uygulanır (Mica→Acrylic→fallback zinciri).
		window.HandlerChanged += (_, _) =>
		{
			try
			{
				BackdropService.ApplyTo(window);
			}
			catch (Exception ex)
			{
				Log("Backdrop error: " + ex.ToString());
			}
		};

		_ = InitializeAsync(window);

		return window;
	}    private async Task InitializeAsync(Window window)
    {
        try
        {			await _syncService.InitializeAsync();

            // Çevrimdışı Whisper ses tanıma modelini arka planda önceden indir
            // (ilk kullanımda mikrofon akışı beklemesin; yoksa tek seferlik indirme).
            _ = PreloadSpeechModelAsync();

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

    private async Task PreloadSpeechModelAsync()
    {
        try
        {
            var stt = IPlatformApplication.Current?.Services.GetService<Services.SpeechToTextService>();
            if (stt == null)
                return;

            if (stt.IsModelReady)
            {
                Log("STT: whisper model hazır (önbellek)");
                return;
            }

            var ok = await stt.EnsureModelAsync();
            Log(ok ? "STT: whisper model indirildi" : "STT: model indirilemedi (çevrimdışı olabilir)");
        }
        catch (Exception ex)
        {
            Log("STT preload error: " + ex.ToString());
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
