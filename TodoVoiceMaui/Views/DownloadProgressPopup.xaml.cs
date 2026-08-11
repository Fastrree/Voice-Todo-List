using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

/// <summary>
/// Model indirme detay modalı. BindingContext = SettingsPageViewModel olduğundan
/// PropertyChanged ile % / MB / hız canlı güncellenir; indirme bittiğinde (veya
/// iptal edildiğinde) kendini kapatır. Dışa tıklanırsa arka planda devam eder.
/// </summary>
public partial class DownloadProgressPopup : Popup
{
    private readonly SettingsPageViewModel _viewModel;

    public DownloadProgressPopup(SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        // Popup kapanınca (Close / dışa tıklama) aboneliği çöz — sızıntı yok
        Closed += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // İndirme tamamlandı/iptal edildi → modal kendini kapatır
        if (e.PropertyName == nameof(SettingsPageViewModel.IsSttDownloading) && !_viewModel.IsSttDownloading)
        {
            Close();
        }
    }
}
