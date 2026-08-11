using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class TodoDetailPage : ContentPage
{
    private readonly TodoDetailPageViewModel _viewModel;
    private bool _hasAnimated;

    public TodoDetailPage(TodoDetailPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            if (DetailScroll != null)
                await AnimationService.FadeSlideInAsync(DetailScroll, 0, 380, 18);
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        SoundEffectService.Play(SoundEffectService.SoundKind.Click);
        await Shell.Current.GoToAsync("..");
    }
}
