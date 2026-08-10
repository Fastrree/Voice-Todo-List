using Microsoft.UI.Xaml;

namespace TodoVoiceMaui.WinUI;

public static class Program
{
    [global::System.STAThreadAttribute]
    static void Main(string[] args)
    {
        try
        {
            global::WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.AppContext.BaseDirectory, "startup-error.log"),
                ex.ToString());
            throw;
        }
    }
}
