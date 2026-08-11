using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class TodoListPage : ContentPage
{
    private readonly TodoListPageViewModel _viewModel;
    private AnimationService.BreathHandle? _micBreath;
    private bool _hasAnimated;

    public TodoListPage(TodoListPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        await _viewModel.InitializeAsync();
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            _ = PlayEntranceAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopMicBreathing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private async Task PlayEntranceAsync()
    {
        // Kontrollü giriş: üstten alta hafif akış (transition-framework §4)
        if (HeaderGrid != null)
            await AnimationService.FadeSlideInAsync(HeaderGrid, 0, 320, 14);
        if (SearchBox != null)
            await AnimationService.FadeSlideInAsync(SearchBox, 40, 320, 14);
        if (FiltersScroll != null)
            await AnimationService.FadeSlideInAsync(FiltersScroll, 80, 320, 14);
        if (TodosRefresh != null)
            await AnimationService.FadeSlideInAsync(TodosRefresh, 120, 400, 18);
        if (BottomBar != null)
            await AnimationService.FadeSlideInAsync(BottomBar, 160, 400, 16);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Dinleme durumunda mikrofon nefes alır (kayıt açıkken görsel geri bildirim)
        if (e.PropertyName == nameof(TodoListPageViewModel.IsSpeechListening))
        {
            if (_viewModel.IsSpeechListening)
                StartMicBreathing();
            else
                StopMicBreathing();
        }
    }

    private void StartMicBreathing()
    {
        if (_micBreath != null || MicButton == null)
            return;
        _micBreath = AnimationService.Breathe(MicButton, 1.0, 1.12, 620);
        _micBreath.Start();
    }

    private void StopMicBreathing()
    {
        _micBreath?.Stop();
        _micBreath = null;
    }

    // Satır hover: yalnızca translate (ölçek yok — sanallaştırılan satırlarda state taşmaz)
    private async void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Element element && element.Parent is VisualElement row)
        {
            row.CancelAnimations();
            await row.TranslateTo(0, -2, 150, AnimationService.EaseOutCubic);
        }
    }

    private async void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Element element && element.Parent is VisualElement row)
        {
            row.CancelAnimations();
            await row.TranslateTo(0, 0, 150, AnimationService.EaseOutCubic);
        }
    }

    private void OnPriorityPickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.PriorityFilterOptions[picker.SelectedIndex];
            _viewModel.ChangePriorityCommand.Execute(item.Key);
        }
    }

    private void OnSortPickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.SortOptions[picker.SelectedIndex];
            _viewModel.ChangeSortCommand.Execute(item.Key);
        }
    }
}
