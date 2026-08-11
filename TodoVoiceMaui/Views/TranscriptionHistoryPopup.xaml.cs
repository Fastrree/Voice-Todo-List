using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TodoVoiceMaui.Services;

namespace TodoVoiceMaui.Views;

/// <summary>
/// Transkripsiyon geçmişi modalı: son ses tanımaları listeler; kullanıcı bir
/// kaydı düzeltince metin güncellenir ve kelime çiftleri kullanıcı sözlüğüne
/// öğrenilir (TurkishVocabulary) — zamanla kişiye özel tanıma.
/// </summary>
public partial class TranscriptionHistoryPopup : Popup
{
    private readonly TranscriptionHistoryPopupViewModel _viewModel;

    public TranscriptionHistoryPopup()
    {
        InitializeComponent();
        _viewModel = new TranscriptionHistoryPopupViewModel();
        BindingContext = _viewModel;
        _viewModel.RequestClose += () => Close();
        // Statik servis event'ine abonelik popup kapandığında bırakılır (sızıntı yok)
        Closed += (_, _) => _viewModel.Dispose();
    }
}

public partial class TranscriptionHistoryPopupViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<HistoryRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> UserWords { get; } = new();

    [ObservableProperty]
    private string summaryText = string.Empty;

    [ObservableProperty]
    private string wordCountText = string.Empty;

    public event Action? RequestClose;

    public TranscriptionHistoryPopupViewModel()
    {
        Reload();
        TranscriptionHistoryService.Changed += OnHistoryChanged;
    }

    public void Dispose()
        => TranscriptionHistoryService.Changed -= OnHistoryChanged;

    private void OnHistoryChanged() => Reload();

    private void Reload()
    {
        Rows.Clear();
        foreach (var entry in TranscriptionHistoryService.GetAll())
            Rows.Add(new HistoryRowViewModel(entry));

        SummaryText = Rows.Count == 0
            ? "Henüz kayıt yok"
            : $"Son {Rows.Count} transkripsiyon";

        ReloadWords();
    }

    private void ReloadWords()
    {
        UserWords.Clear();
        foreach (var word in TurkishVocabulary.GetUserWords())
            UserWords.Add(word);

        WordCountText = UserWords.Count == 0
            ? "Öğrenilen kelime yok"
            : $"{UserWords.Count} öğrenilen kelime";
    }

    [RelayCommand]
    private void EditRow(HistoryRowViewModel row)
    {
        row.HasError = false;
        row.ErrorText = string.Empty;
        row.EditText = row.DisplayText;
        row.IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit(HistoryRowViewModel row)
    {
        row.IsEditing = false;
        row.HasError = false;
        row.ErrorText = string.Empty;
    }

    [RelayCommand]
    private void SaveCorrection(HistoryRowViewModel row)
    {
        var corrected = row.EditText?.Trim() ?? string.Empty;
        if (corrected.Length == 0)
        {
            row.HasError = true;
            row.ErrorText = "Düzeltme boş olamaz.";
            return;
        }

        row.HasError = false;
        row.ErrorText = string.Empty;
        row.IsEditing = false;

        // Metni günceller + kelime çiftlerini kullanıcı sözlüğüne öğretir
        TranscriptionHistoryService.Correct(row.Id, corrected);
        SoundEffectService.Play(SoundEffectService.SoundKind.Success);
    }

    [RelayCommand]
    private void DeleteRow(HistoryRowViewModel row)
        => TranscriptionHistoryService.Remove(row.Id);

    [RelayCommand]
    private void ClearAll()
        => TranscriptionHistoryService.Clear();

    [RelayCommand]
    private void RemoveUserWord(string word)
        => TurkishVocabulary.RemoveUserWord(word);

    [RelayCommand]
    private void Close()
        => RequestClose?.Invoke();
}

/// <summary>Geçmiş listesindeki tek satır (düzenleme durumuyla).</summary>
public partial class HistoryRowViewModel : ObservableObject
{
    public string Id { get; }
    public string Provider { get; }
    public DateTime CreatedAt { get; }

    public string DisplayText { get; private set; }

    public string TimeLabel => CreatedAt.ToString(
        "dd MMM · HH:mm", new System.Globalization.CultureInfo("tr-TR"));

    public string ProviderLabel => Provider switch
    {
        "whisper-offline" => "Çevrimdışı Whisper",
        "openai" => "OpenAI",
        "google" => "Google",
        "azure" => "Azure",
        "deepgram" => "Deepgram",
        "assemblyai" => "AssemblyAI",
        "elevenlabs" => "ElevenLabs",
        "whisper-api" => "Whisper API",
        var p when string.IsNullOrEmpty(p) => "Ses",
        var p => p
    };

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editText = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorText = string.Empty;

    public HistoryRowViewModel(TranscriptionEntry entry)
    {
        Id = entry.Id;
        Provider = entry.Provider;
        CreatedAt = entry.CreatedAt;
        DisplayText = entry.CorrectedText ?? entry.Text;
        EditText = DisplayText;
    }
}
