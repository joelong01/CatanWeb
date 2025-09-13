using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

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
}