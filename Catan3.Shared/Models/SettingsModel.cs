using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Net.Http;
using System.IO;

namespace Catan3.Shared.Models
{
    public partial class SettingItem : ObservableObject
    {
        [ObservableProperty]
        [JsonPropertyName("settingName")]
        public partial string SettingName { get; set; } = string.Empty;

        [ObservableProperty]
        [JsonPropertyName("description")]
        public partial string Description { get; set; } = string.Empty;

        [ObservableProperty]
        [JsonPropertyName("inputType")]
        public partial string InputType { get; set; } = "textbox"; // textbox, dropdown, checkbox

        [ObservableProperty]
        [JsonPropertyName("options")]
        public partial string[]? Options { get; set; }

        [ObservableProperty]
        [JsonPropertyName("value")]
        public partial object? Value { get; set; }

        [ObservableProperty]
        [JsonPropertyName("defaultValue")]
        public partial object? DefaultValue { get; set; }

        [ObservableProperty]
        [JsonPropertyName("validation")]
        public partial ValidationRules? Validation { get; set; }

        [ObservableProperty]
        [JsonPropertyName("environmentVariable")]
        public partial string? EnvironmentVariable { get; set; }

        /// <summary>
        /// Gets the value as a string, handling null and type conversion
        /// </summary>
        public string ValueAsString => Value?.ToString() ?? string.Empty;

        /// <summary>
        /// Gets the value as an integer, with fallback to default
        /// </summary>
        public int ValueAsInt => Value is int intVal ? intVal : (DefaultValue is int defInt ? defInt : 0);

        /// <summary>
        /// Gets the value as a boolean, with fallback to default
        /// </summary>
        public bool ValueAsBool => Value is bool boolVal ? boolVal : (DefaultValue is bool defBool ? defBool : false);

        /// <summary>
        /// Gets or sets the text value for UI binding with two-way binding
        /// </summary>
        [ObservableProperty]
        public partial string TextValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the boolean value for UI binding with two-way binding
        /// </summary>
        [ObservableProperty]
        public partial bool BooleanValue { get; set; } = false;

        /// <summary>
        /// Gets or sets whether this setting has a validation error
        /// </summary>
        [ObservableProperty]
        public partial bool HasValidationError { get; set; } = false;

        /// <summary>
        /// Gets or sets the validation error message for this setting
        /// </summary>
        [ObservableProperty]
        public partial string ValidationErrorMessage { get; set; } = string.Empty;


        /// <summary>
        /// Called when TextValue changes - updates the underlying Value
        /// </summary>
        partial void OnTextValueChanged(string oldValue, string newValue)
        {
            SetValueFromString(newValue);
        }

        /// <summary>
        /// Called when BooleanValue changes - updates the underlying Value
        /// </summary>
        partial void OnBooleanValueChanged(bool oldValue, bool newValue)
        {
            Value = newValue;
        }

        /// <summary>
        /// Called when Value changes - updates the binding properties
        /// </summary>
        partial void OnValueChanged(object? oldValue, object? newValue)
        {
            // Update binding properties when the underlying Value changes
            TextValue = ValueAsString;
            BooleanValue = ValueAsBool;
        }

        /// <summary>
        /// Sets the value from a string, converting to appropriate type
        /// </summary>
        public void SetValueFromString(string stringValue)
        {
            switch (InputType.ToLowerInvariant())
            {
                case "checkbox":
                    Value = bool.TryParse(stringValue, out var boolResult) ? boolResult : DefaultValue;
                    break;
                case "dropdown":
                    if (int.TryParse(stringValue, out var intResult))
                        Value = intResult;
                    else
                        Value = stringValue;
                    break;
                default:
                    Value = stringValue;
                    break;
            }
        }


        public override string ToString()
        {
            return $"[{SettingName}={ValueAsString}]";
        }
    }


    public partial class SettingsModel : ObservableObject
    {
        [ObservableProperty]
        [JsonPropertyName("settings")]
        public partial List<SettingItem> Settings { get; set; } = [];

        /// <summary>
        /// Gets a setting by name
        /// </summary>
        public SettingItem? GetSetting(string settingName)
        {
            return Settings.FirstOrDefault(s => s.SettingName.Equals(settingName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets a setting value as string
        /// </summary>
        public string GetStringValue(string settingName)
        {
            return GetSetting(settingName)?.ValueAsString ?? string.Empty;
        }

        /// <summary>
        /// Gets a setting value as int
        /// </summary>
        public int GetIntValue(string settingName)
        {
            return GetSetting(settingName)?.ValueAsInt ?? 0;
        }

        /// <summary>
        /// Gets a setting value as bool
        /// </summary>
        public bool GetBoolValue(string settingName)
        {
            return GetSetting(settingName)?.ValueAsBool ?? false;
        }

        /// <summary>
        /// Sets a setting value
        /// </summary>
        public void SetValue(string settingName, object value)
        {
            var setting = GetSetting(settingName);
            if (setting != null)
            {
                setting.Value = value;
            }
        }

        /// <summary>
        /// Asynchronously retrieves current settings via MVVM messaging.
        /// Registers for UpdateSettings response, sends GetSettingsMessage, waits for response,
        /// then unregisters to prevent memory leaks.
        /// </summary>
        /// <returns>Task that completes with current settings</returns>
        public static async Task<SettingsModel> GetAsync()
        {
            var tcs = new TaskCompletionSource<SettingsModel>();
            var messenger = WeakReferenceMessenger.Default;

            // Create a temporary recipient for the response
            var tempRecipient = new SettingsRequestRecipient(tcs, messenger);

            // Register the temporary recipient
            messenger.Register<UpdateSettings>(tempRecipient, tempRecipient.HandleUpdateSettings);

            try
            {
                // Send the request
                messenger.Send(new GetSettingsMessage());

                // Wait for the response
                return await tcs.Task;
            }
            catch
            {
                // Ensure cleanup on any exception
                messenger.Unregister<UpdateSettings>(tempRecipient);
                throw;
            }
        }

        /// <summary>
        /// Validates all settings including conditional logic and service reachability
        /// </summary>
        /// <returns>ValidationResult indicating whether all settings are valid</returns>
        public static async Task<ValidationResult> ValidateAsync()
        {
            try
            {
                var settings = await GetAsync();
                return await ValidateSettings(settings);
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Error retrieving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a settings model with all validation rules including service reachability
        /// </summary>
        private static async Task<ValidationResult> ValidateSettings(SettingsModel settings)
        {
            // Check each setting's individual validation
            foreach (var setting in settings.Settings)
            {
                var result = await ValidateIndividualSetting(setting, settings);
                if (!result.IsValid)
                {
                    return result;
                }
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// Validates a single setting with conditional logic and service checks
        /// </summary>
        private static async Task<ValidationResult> ValidateIndividualSetting(SettingItem setting, SettingsModel allSettings)
        {
            var validation = setting.Validation;
            if (validation == null) return new ValidationResult(true, string.Empty);

            var value = setting.ValueAsString;

            // Special case: SaveFileLocation is only required when ServiceGame is false
            if (setting.SettingName == "SaveFileLocation")
            {
                var serviceGameSetting = allSettings.GetSetting("ServiceGame");
                bool usingServiceGame = serviceGameSetting?.ValueAsBool ?? true;

                if (usingServiceGame)
                {
                    // When using service game, SaveFileLocation is not required
                    return new ValidationResult(true, string.Empty);
                }
            }

            // Special case: GameServiceUrl needs reachability check when ServiceGame is true
            if (setting.SettingName == "GameServiceUrl")
            {
                var serviceGameSetting = allSettings.GetSetting("ServiceGame");
                bool usingServiceGame = serviceGameSetting?.ValueAsBool ?? false;

                if (usingServiceGame && !string.IsNullOrWhiteSpace(value))
                {
                    var reachabilityResult = await CheckGameServiceReachability(value);
                    if (!reachabilityResult.IsValid)
                    {
                        return reachabilityResult;
                    }
                }
            }

            // Required field validation
            if (validation.Required && string.IsNullOrWhiteSpace(value))
            {
                return new ValidationResult(false, $"{setting.Description} is required.");
            }

            // Length validation
            if (validation.MinLength.HasValue && value.Length < validation.MinLength.Value)
            {
                return new ValidationResult(false, $"{setting.Description} must be at least {validation.MinLength} characters long.");
            }

            if (validation.MaxLength.HasValue && value.Length > validation.MaxLength.Value)
            {
                return new ValidationResult(false, $"{setting.Description} must be no more than {validation.MaxLength} characters long.");
            }

            // Directory existence validation
            if (validation.DirectoryMustExist && !string.IsNullOrWhiteSpace(value) && !Directory.Exists(value))
            {
                return new ValidationResult(false, $"Directory '{value}' does not exist. Please choose a valid directory for {setting.Description}.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// Checks if the GameService is reachable at the specified URL
        /// </summary>
        private static async Task<ValidationResult> CheckGameServiceReachability(string serviceUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);

                // Try a simple GET to the base URL or health endpoint
                var response = await httpClient.GetAsync(serviceUrl.TrimEnd('/'));

                if (response.IsSuccessStatusCode)
                {
                    return new ValidationResult(true, string.Empty);
                }
                else
                {
                    return new ValidationResult(false, $"GameService at {serviceUrl} returned {response.StatusCode}.");
                }
            }
            catch (HttpRequestException ex)
            {
                return new ValidationResult(false, $"GameService not reachable at {serviceUrl}: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return new ValidationResult(false, $"GameService at {serviceUrl} timed out (3 seconds).");
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Error connecting to GameService at {serviceUrl}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Temporary recipient for settings requests to handle messaging properly
    /// </summary>
    internal class SettingsRequestRecipient
    {
        private readonly TaskCompletionSource<SettingsModel> _tcs;
        private readonly IMessenger _messenger;

        public SettingsRequestRecipient(TaskCompletionSource<SettingsModel> tcs, IMessenger messenger)
        {
            _tcs = tcs;
            _messenger = messenger;
        }

        public void HandleUpdateSettings(object recipient, UpdateSettings message)
        {
            // Unregister to prevent memory leaks
            _messenger.Unregister<UpdateSettings>(this);
            
            // Complete the task with the settings
            _tcs.SetResult(message.Settings);
        }

    }

    public class ValidationRules
    {
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        [JsonPropertyName("minLength")]
        public int? MinLength { get; set; }

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }

        [JsonPropertyName("directoryMustExist")]
        public bool DirectoryMustExist { get; set; }
    }

    /// <summary>
    /// Result of settings validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether the validation passed
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the error message if validation failed
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new ValidationResult
        /// </summary>
        /// <param name="isValid">Whether validation passed</param>
        /// <param name="errorMessage">Error message if validation failed</param>
        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}