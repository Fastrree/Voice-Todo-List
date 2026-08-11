using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private AnimationService.BreathHandle? _breath;
    private bool _hasAnimated;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadStatsAsync();
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            _ = PlayEntranceAsync();
        }
        else
        {
            // Sonraki sekmelerde seremoni tekrarlanmaz; mikrofon yine nefes alır
            StartBreathing();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopBreathing();
    }

    private async Task PlayEntranceAsync()
    {
        // Karşılama seremonisi (transition-framework §4): hero önce, istatistikler sonra,
        // mikrofon nefes almaya başlar — örtüşen timeline, donuk an yok.
        if (HeroSection != null)
            await AnimationService.FadeSlideInAsync(HeroSection, 0, 420, 18);
        if (StatsSection != null)
            await AnimationService.FadeSlideInAsync(StatsSection, 100, 420, 20);

        StartBreathing();
    }

    private void StartBreathing()
    {
        if (_breath != null || MicRingGrid == null)
            return;
        _breath = AnimationService.Breathe(MicRingGrid, 1.0, 1.05, 1100);
        _breath.Start();
    }

    private void StopBreathing()
    {
        _breath?.Stop();
        _breath = null;
    }

    private async void OnStatPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Element element && element.Parent is VisualElement card)
            await AnimationService.LiftAsync(card);
    }

    private async void OnStatPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Element element && element.Parent is VisualElement card)
            await AnimationService.UnliftAsync(card);
    }
}
