using TodoVoiceMaui.Models;

namespace TodoVoiceMaui.Services;

public class ReminderService : IDisposable
{
    private readonly ITodoStore _todoStore;
    private CancellationTokenSource? _cts;
    private readonly HashSet<string> _firedReminders = new();

    public ReminderService(ITodoStore todoStore)
    {
        _todoStore = todoStore;
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await CheckRemindersAsync();
                await Task.Delay(TimeSpan.FromSeconds(15), token);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reminder loop error: {ex.Message}");
        }
    }

    private async Task CheckRemindersAsync()
    {
        try
        {
            var todos = await _todoStore.GetTodosAsync();
            var now = DateTime.Now;

            foreach (var todo in todos)
            {
                if (todo.ReminderAt.HasValue && !todo.Completed && !_firedReminders.Contains(todo.Id))
                {
                    var reminderLocal = todo.ReminderAt.Value;
                    if (reminderLocal <= now)
                    {
                        _firedReminders.Add(todo.Id);
                        ShowReminder(todo.Title);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reminder check error: {ex.Message}");
        }
    }

    public void MarkFired(string todoId)
    {
        _firedReminders.Add(todoId);
    }

    private void ShowReminder(string todoTitle)
    {
        // Sesli bildirim — yumuşak davet tonu (Ayarlar'daki ses anahtarına uyar)
        SoundEffectService.Play(SoundEffectService.SoundKind.Reminder);

#if WINDOWS
        try
        {
            var notifier = Windows.UI.Notifications.ToastNotificationManager
                .CreateToastNotifier();
            var xml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>Görev Hatırlatıcısı</text>
                            <text>{System.Security.SecurityElement.Escape(todoTitle)}</text>
                        </binding>
                    </visual>
                </toast>";
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            notifier.Show(new Windows.UI.Notifications.ToastNotification(doc));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast failed: {ex.Message}");
        }
#else
        System.Diagnostics.Debug.WriteLine($"Reminder (non-Windows): {todoTitle}");
#endif
    }

    public void Dispose()
    {
        Stop();
    }
}
