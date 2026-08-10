using Microsoft.UI.Xaml;

namespace TodoVoiceMaui.WinUI;

public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
		this.UnhandledException += (s, e) =>
		{
			try
			{
				System.IO.File.AppendAllText(
					System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
					"WinUI UnhandledException: " + e.Exception?.ToString() + Environment.NewLine);
			}
			catch { }
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
