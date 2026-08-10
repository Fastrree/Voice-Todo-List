using Supabase;
using TodoVoiceMaui.Models;
using Newtonsoft.Json;
using System.Net.Http;

namespace TodoVoiceMaui.Services;

public class SupabaseService
{
    private readonly HttpClient _httpClient;
    private Client? _client;
    private const string SUPABASE_URL = "http://127.0.0.1:54321";
    private const string SUPABASE_ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";

    public SupabaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public event EventHandler<Supabase.Gotrue.User>? UserChanged;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public async Task InitializeAsync()
    {
        try
        {
            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false
            };

            _client = new Client(SUPABASE_URL, SUPABASE_ANON_KEY, options);
            await _client.InitializeAsync();

            // Set up auth state change listener
            _client.Auth.AddStateChangedListener((sender, state) =>
            {
                UserChanged?.Invoke(this, _client.Auth.CurrentUser);
            });

            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Supabase initialization failed: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, false);
            throw;
        }
    }

    public Client Client => _client ?? throw new InvalidOperationException("Supabase client not initialized");

    // Authentication methods
    public async Task<bool> SignUpAsync(string email, string password)
    {
        try
        {
            var result = await Client.Auth.SignUp(email, password);
            return result.User != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sign up failed: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> SignInAsync(string email, string password)
    {
        try
        {
            var result = await Client.Auth.SignIn(email, password);
            return result.User != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sign in failed: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> SignOutAsync()
    {
        try
        {
            await Client.Auth.SignOut();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sign out failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsUserLoggedInAsync()
    {
        try
        {
            var session = Client.Auth.CurrentSession;
            var user = Client.Auth.CurrentUser;
            return session != null && user != null;
        }
        catch
        {
            return false;
        }
    }

    public Supabase.Gotrue.User? GetCurrentUser()
    {
        return Client.Auth.CurrentUser;
    }

    // Edge function calls - direct HTTP to avoid SDK URL-prefix bug
    public async Task<T?> InvokeFunctionAsync<T>(string functionName, object? parameters = null)
    {
        try
        {
            var url = $"{SUPABASE_URL}/functions/v1/{functionName}";
            var token = Client.Auth.CurrentSession?.AccessToken ?? SUPABASE_ANON_KEY;

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("apikey", SUPABASE_ANON_KEY);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            var body = parameters == null
                ? new Dictionary<string, object>()
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(parameters)) ?? new Dictionary<string, object>();
            request.Content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(content))
            {
                var deserialized = JsonConvert.DeserializeObject<T>(content);
                return deserialized;
            }

            System.Diagnostics.Debug.WriteLine($"Function {functionName} HTTP {(int)response.StatusCode}: {content}");
            return default;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Function {functionName} failed: {ex.Message}");
            throw;
        }
    }

    // Todo operations via edge functions
    public async Task<TodoDto?> CreateTodoAsync(string title, string? description = null, string priority = "medium", DateTime? dueDate = null, string? voiceUrl = null, int? voiceDuration = null)
    {
        var parameters = new
        {
            operation = "create",
            todoData = new
            {
                title,
                description,
                priority,
                dueDate = dueDate?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            voiceData = voiceUrl != null ? new { publicUrl = voiceUrl, duration = voiceDuration } : null
        };

        var result = await InvokeFunctionAsync<TodoOperationResult>("todo-manager", parameters);
        return result?.Todo;
    }

    public async Task<TodoDto?> UpdateTodoAsync(string todoId, object updates)
    {
        var parameters = new
        {
            operation = "update",
            todoId,
            todoData = updates
        };

        var result = await InvokeFunctionAsync<TodoOperationResult>("todo-manager", parameters);
        return result?.Todo;
    }

    public async Task<bool> DeleteTodoAsync(string todoId)
    {
        var parameters = new
        {
            operation = "delete",
            todoId
        };

        var result = await InvokeFunctionAsync<TodoOperationResult>("todo-manager", parameters);
        return result?.Operation == "deleted";
    }

    public async Task<List<TodoDto>> GetTodosAsync(bool? completed = null)
    {
        var parameters = new
        {
            operation = "list",
            todoData = completed.HasValue ? new { completed } : null
        };

        var result = await InvokeFunctionAsync<TodoListResult>("todo-manager", parameters);
        return result?.Todos ?? new List<TodoDto>();
    }

    public async Task<(TodoDto?, List<VoiceRecording>)> GetTodoWithVoiceAsync(string todoId)
    {
        var parameters = new
        {
            operation = "get",
            todoId
        };

        var result = await InvokeFunctionAsync<TodoDetailResult>("todo-manager", parameters);
        return (result?.Todo, result?.VoiceRecordings ?? new List<VoiceRecording>());
    }

    // Voice recording operations
    public async Task<VoiceRecording?> UploadVoiceRecordingAsync(string audioData, string fileName, string? todoId = null, int? duration = null)
    {
        var parameters = new
        {
            audioData,
            fileName,
            todoId,
            duration
        };

        var result = await InvokeFunctionAsync<VoiceUploadResult>("voice-upload", parameters);
        return result?.VoiceRecording;
    }

    // User profile operations
    public async Task<UserProfile?> GetOrCreateProfileAsync(string? fullName = null, Dictionary<string, object>? preferences = null)
    {
        var parameters = new
        {
            operation = "get_or_create",
            profileData = new { fullName, preferences }
        };

        var result = await InvokeFunctionAsync<ProfileOperationResult>("user-profile", parameters);
        return result?.Profile;
    }

    public async Task<UserProfile?> UpdateProfileAsync(object updates)
    {
        var parameters = new
        {
            operation = "update",
            profileData = updates
        };

        var result = await InvokeFunctionAsync<ProfileOperationResult>("user-profile", parameters);
        return result?.Profile;
    }

    public async Task<UserStats?> GetUserStatsAsync()
    {
        var parameters = new
        {
            operation = "get_stats"
        };

        var result = await InvokeFunctionAsync<UserStatsResult>("user-profile", parameters);
        return result?.Stats;
    }
}

// Response models for edge functions
public class FunctionResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
}

public class FunctionErrorResponse
{
    public ErrorInfo? Error { get; set; }
    public bool Success { get; set; }
}

public class ErrorInfo
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public class TodoOperationResult
{
    public TodoDto? Todo { get; set; }
    public string? Operation { get; set; }
}

public class TodoListResult
{
    public List<TodoDto>? Todos { get; set; }
    public string? Operation { get; set; }
}

public class TodoDetailResult
{
    public TodoDto? Todo { get; set; }
    public List<VoiceRecording>? VoiceRecordings { get; set; }
    public string? Operation { get; set; }
}

public class VoiceUploadResult
{
    public VoiceRecording? VoiceRecording { get; set; }
    public string? PublicUrl { get; set; }
    public string? StoragePath { get; set; }
}

public class ProfileOperationResult
{
    public UserProfile? Profile { get; set; }
    public string? Operation { get; set; }
}

public class UserStatsResult
{
    public UserStats? Stats { get; set; }
    public string? Operation { get; set; }
}

public class UserStats
{
    [JsonProperty("total_todos")]
    public int TotalTodos { get; set; }
    [JsonProperty("completed_todos")]
    public int CompletedTodos { get; set; }
    [JsonProperty("pending_todos")]
    public int PendingTodos { get; set; }
    [JsonProperty("todos_with_voice")]
    public int TodosWithVoice { get; set; }
    [JsonProperty("total_voice_recordings")]
    public int TotalVoiceRecordings { get; set; }
    [JsonProperty("total_voice_duration")]
    public int TotalVoiceDuration { get; set; }
    [JsonProperty("todos_this_week")]
    public int TodosThisWeek { get; set; }
    [JsonProperty("completion_rate")]
    public int CompletionRate { get; set; }
}