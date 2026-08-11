using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginPageViewModel _viewModel;
    private bool _hasAnimated;

    public LoginPage(LoginPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        
        // Setup button commands since XAML converters are complex
        SetupButtonCommands();
    }

    private void SetupButtonCommands()
    {
        // Handle primary button command based on mode
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LoginPageViewModel.IsSignupMode))
            {
                UpdatePrimaryButtonCommand();
            }
        };
        
        UpdatePrimaryButtonCommand();
    }

    private void UpdatePrimaryButtonCommand()
    {
        // Find the primary button and update its command
        if (this.FindByName("PrimaryButton") is Button primaryButton)
        {
            primaryButton.Command = _viewModel.IsSignupMode 
                ? _viewModel.SignUpCommand 
                : _viewModel.SignInCommand;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            if (LoginContainer != null)
                await AnimationService.FadeSlideInAsync(LoginContainer, 0, 450, 24);
        }
    }
}
