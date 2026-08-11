namespace TodoVoiceMaui.Services;

/// <summary>
/// Sayfa girişi + mikro-etkileşim animasyonları (transition-framework.md §3).
/// Yalnızca Transform (Scale/Translate) + Opacity kullanır — layout animasyonu yasak.
/// </summary>
public static class AnimationService
{
    /// <summary>Apple hissi ana eğri: easeOutQuint (yumuşak yavaşlama).</summary>
    public static readonly Easing EaseOutQuint = new Easing(t => 1 - Math.Pow(1 - t, 5));

    /// <summary>Küçük elementler için: easeOutCubic.</summary>
    public static readonly Easing EaseOutCubic = new Easing(t => 1 - Math.Pow(1 - t, 3));

    /// <summary>Fade + yukarı yükselme girişi (sayfa/hero/kart).</summary>
    public static async Task FadeSlideInAsync(VisualElement view, int delayMs = 0, uint durationMs = 380, double rise = 16)
    {
        if (view == null)
            return;

        view.Opacity = 0;
        view.TranslationY = rise;

        if (delayMs > 0)
            await Task.Delay(delayMs);

        await Task.WhenAll(
            view.FadeTo(1, durationMs, EaseOutQuint),
            view.TranslateTo(0, 0, durationMs, EaseOutQuint));
    }

    /// <summary>Hover'da hafif yükselme (kartlar). Çıkışta geri döner.</summary>
    public static Task LiftAsync(VisualElement view)
        => Task.WhenAll(
            view.ScaleTo(1.015, 150, EaseOutCubic),
            view.TranslateTo(0, -2, 150, EaseOutCubic));

    public static Task UnliftAsync(VisualElement view)
        => Task.WhenAll(
            view.ScaleTo(1.0, 150, EaseOutCubic),
            view.TranslateTo(0, 0, 150, EaseOutCubic));

    /// <summary>
    /// Sonsuz "nefes" döngüsü (mikrofon halkası / dinleme durumu).
    /// Stop() çağrılana veya animasyon iptal edilene kadar sürer.
    /// </summary>
    public sealed class BreathHandle
    {
        private readonly VisualElement _view;
        private readonly double _from;
        private readonly double _to;
        private readonly uint _durationMs;
        private volatile bool _running;
        private readonly object _sync = new();

        public BreathHandle(VisualElement view, double from, double to, uint durationMs = 900)
        {
            _view = view;
            _from = from;
            _to = to;
            _durationMs = durationMs;
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_running)
                    return;
                _running = true;
            }
            Loop();
        }

        public void Stop()
        {
            lock (_sync)
            {
                _running = false;
            }
            _view.CancelAnimations();
            _view.Scale = _from;
        }

        private async void Loop()
        {
            while (_running)
            {
                try
                {
                    await _view.ScaleTo(_to, _durationMs, Easing.SinInOut);
                    if (!_running)
                        break;
                    await _view.ScaleTo(_from, _durationMs, Easing.SinInOut);
                }
                catch
                {
                    // Sayfa kapanırken animasyon iptal edilebilir — sessizce çık
                    break;
                }
            }
        }
    }

    public static BreathHandle Breathe(VisualElement view, double from, double to, uint durationMs = 900)
        => new(view, from, to, durationMs);
}
