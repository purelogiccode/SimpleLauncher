using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.PlaySound;
using Application = System.Windows.Application;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the support request submission window.
/// </summary>
public partial class SupportViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    [ObservableProperty] public partial string Email { get; set; } = "";

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = "";

    [ObservableProperty] public partial string SupportRequest { get; set; } = "";

    /// <summary>Initializes a new instance of the <see cref="SupportViewModel" /> class.</summary>
    /// <param name="playSoundEffects">The sound effects service for playing notification sounds.</param>
    /// <param name="httpClientFactory">The HTTP client factory for sending support requests.</param>
    /// <param name="configuration">The application configuration for API settings.</param>
    /// <param name="messageBox">The message box service for displaying dialogs.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    /// <param name="logger">The logger for recording errors and debug information.</param>
    public SupportViewModel(PlaySoundEffects playSoundEffects, IHttpClientFactory httpClientFactory,
        IConfiguration configuration, IMessageBoxLibraryService messageBox, IResourceProvider resourceProvider,
        ILogger logger)
    {
        _playSoundEffects = playSoundEffects;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;
        _logger = logger;
    }

    /// <summary>Event raised when the window should be closed.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Event raised when the form fields have been cleared after successful submission.</summary>
    public event EventHandler? FormCleared;

    [RelayCommand]
    private async Task SendSupportRequestAsync()
    {
        _logger.Debug("[Support] SendSupportRequestAsync started");

        if (string.IsNullOrWhiteSpace(Name))
        {
            _logger.Debug("[Support] Validation failed: Name is empty");
            await _messageBox.EnterNameMessageBoxAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            _logger.Debug("[Support] Validation failed: Email is empty");
            await _messageBox.EnterEmailMessageBoxAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(SupportRequest))
        {
            _logger.Debug("[Support] Validation failed: SupportRequest is empty");
            await _messageBox.EnterSupportRequestMessageBoxAsync();
            return;
        }

        _logger.Debug(
            $"[Support] Validation passed. Name='{Name}', Email='{Email}', MessageLength={SupportRequest.Length}");
        IsLoading = true;

        try
        {
            var fullMessageBuilder = new StringBuilder();
            fullMessageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Name: {Name}");
            fullMessageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Email: {Email}");
            fullMessageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Support Request:\n\n{SupportRequest}");

            _playSoundEffects.PlayNotificationSound();
            _logger.Debug("[Support] Calling SendSupportRequestToApiAsync...");
            await SendSupportRequestToApiAsync(fullMessageBuilder.ToString());

            (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                _resourceProvider.GetString("SendingSupportRequest", "Sending support request..."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[Support] Exception in SendSupportRequestAsync");
            _logger.Error(ex, "Error in the SendSupportRequestClickAsync method");
        }
        finally
        {
            IsLoading = false;
            _logger.Debug("[Support] SendSupportRequestAsync finished. IsLoading set to false");
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task SendSupportRequestToApiAsync(string fullMessage)
    {
        var apiBaseUrl = _configuration.GetValue<string>("EmailApiBaseUrl") ??
                         "https://www.purelogiccode.com/customeremailservice/api/send-customer-email/";
        var apiKey = AppConstants.GetApiKey();
        var supportEmailTo = _configuration.GetValue<string>("SupportEmailTo") ?? "contact@purelogiccode.com";

        _logger.Debug($"[Support] EmailApiBaseUrl from config: '{apiBaseUrl}'");
        _logger.Debug(
            $"[Support] ApiKey: '{apiKey.Substring(0, Math.Min(10, apiKey.Length))}...' (length={apiKey.Length})");
        _logger.Debug($"[Support] SupportEmailTo from config: '{supportEmailTo}'");
        _logger.Debug($"[Support] Message body length: {fullMessage.Length} chars");

        var requestPayload = new
        {
            to = supportEmailTo,
            subject = "Support Request from SimpleLauncher",
            body = fullMessage,
            applicationName = "SimpleLauncher",
            isHtml = false
        };

        var jsonString = JsonSerializer.Serialize(requestPayload);
        _logger.Debug($"[Support] JSON payload size: {jsonString.Length} chars");
        var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

        try
        {
            var httpClient = _httpClientFactory?.CreateClient("SupportWindowClient");
            if (httpClient == null)
            {
                _logger.Debug(
                    "[Support] ERROR: httpClient is null. IHttpClientFactory returned null for 'SupportWindowClient'");
                return;
            }

            _logger.Debug(
                $"[Support] HttpClient created. BaseAddress: '{httpClient.BaseAddress}', Timeout: {httpClient.Timeout}");

            var apiUrl = apiBaseUrl.TrimEnd('/');
            _logger.Debug($"[Support] Final API URL: '{apiUrl}'");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = jsonContent;
            request.Headers.Add("X-API-KEY", apiKey);

            _logger.Debug("[Support] Sending HTTP POST request...");
            var stopwatch = Stopwatch.StartNew();

            using var response = await httpClient.SendAsync(request, cts.Token);

            stopwatch.Stop();
            _logger.Debug($"[Support] Response received in {stopwatch.ElapsedMilliseconds}ms.");
            _logger.Debug($"[Support] StatusCode: {(int)response.StatusCode} ({response.StatusCode})");
            _logger.Debug("[Support] Response Headers:");
            foreach (var header in response.Headers)
                _logger.Debug($"[Support]   {header.Key}: {string.Join(", ", header.Value)}");

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                    _logger.Debug($"[Support]   {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (response.Content != null)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.Debug($"[Support] Response body ({responseContent.Length} chars): '{responseContent}'");

                if (response.IsSuccessStatusCode)
                {
                    _logger.Debug("[Support] SUCCESS: Email sent successfully");

                    Name = "";
                    Email = "";
                    SupportRequest = "";

                    FormCleared?.Invoke(this, EventArgs.Empty);

                    await _messageBox.SupportRequestSuccessMessageBoxAsync();
                }
                else
                {
                    _logger.Debug(
                        $"[Support] FAILURE: API returned error. Status={response.StatusCode}, Body='{responseContent}'");

                    var contextMessage =
                        $"An error occurred while sending the Support Request. Status: {response.StatusCode}, Details: {responseContent}";
                    _logger.Warning(contextMessage);

                    await _messageBox.SupportRequestSendErrorMessageBoxAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("[Support] TIMEOUT: Request timed out after 20 seconds");
            _logger.Warning(
                "The support request timed out after 20 seconds. Please check your internet connection and try again");

            await _messageBox.SupportRequestSendErrorMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[Support] EXCEPTION: Error sending the Support Request");
            _logger.Error(ex, "Error sending the Support Request");

            await _messageBox.SupportRequestSendErrorMessageBoxAsync();
        }
    }
}