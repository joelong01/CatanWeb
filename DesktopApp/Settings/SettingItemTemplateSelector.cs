using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace Catan3.Settings
{
    /// <summary>
    /// Template selector that chooses the appropriate DataTemplate based on
    /// the setting's InputType. This eliminates the need for visibility toggles
    /// and creates the exact UI needed for each setting type.
    /// </summary>
    public class SettingItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the template for textbox input settings
        /// </summary>
        public DataTemplate? TextBoxTemplate { get; set; } = null;

        /// <summary>
        /// Gets or sets the template for directory picker settings
        /// </summary>
        public DataTemplate? DirectoryPickerTemplate { get; set; } = null;

        /// <summary>
        /// Gets or sets the template for dropdown/combobox settings
        /// </summary>
        public DataTemplate? DropdownTemplate { get; set; } = null;

        /// <summary>
        /// Gets or sets the template for checkbox settings
        /// </summary>
        public DataTemplate? CheckboxTemplate { get; set; } = null;

        /// <summary>
        /// Selects the appropriate template based on the SettingItem's InputType
        /// </summary>
        /// <param name="item">The SettingItem</param>
        /// <param name="container">The container dependency object</param>
        /// <returns>The DataTemplate to use for rendering this setting</returns>
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            // WinUI sometimes calls this with null during initialization/measure passes
            if (item == null)
            {
                this.TraceMessage("SelectTemplateCore called with null item - returning TextBoxTemplate");
                return TextBoxTemplate ?? base.SelectTemplateCore(item, container);
            }

            this.TraceMessage($"SelectTemplateCore called with item type: {item.GetType().Name}");
            Debug.Assert(container is not null, "Container should not be null");

            // Check for SettingItemViewModel first
            if (item is SettingItemViewModel settingItemViewModel)
            {
                var inputType = settingItemViewModel.InputType?.ToLowerInvariant();
                var isDirectoryPicker = settingItemViewModel.Model.Validation?.DirectoryMustExist == true;

                this.TraceMessage($"SettingItemViewModel: {settingItemViewModel.SettingName}, InputType: '{inputType}', IsDirectoryPicker: {isDirectoryPicker}");

                var selectedTemplate = inputType switch
                {
                    "textbox" when isDirectoryPicker => DirectoryPickerTemplate,
                    "textbox" => TextBoxTemplate,
                    "dropdown" => DropdownTemplate,
                    "checkbox" => CheckboxTemplate,
                    _ => TextBoxTemplate // fallback to textbox template
                };

                this.TraceMessage($"Selected template: {selectedTemplate?.GetType().Name ?? "null"}");
                return selectedTemplate ?? base.SelectTemplateCore(item, container);
            }

            // Fallback for legacy SettingItem usage
            if (item is Catan3.Shared.Models.SettingItem settingItem)
            {
                var inputType = settingItem.InputType?.ToLowerInvariant();
                var isDirectoryPicker = settingItem.Validation?.DirectoryMustExist == true;

                this.TraceMessage($"SettingItem: {settingItem.SettingName}, InputType: '{inputType}', IsDirectoryPicker: {isDirectoryPicker}");

                var selectedTemplate = inputType switch
                {
                    "textbox" when isDirectoryPicker => DirectoryPickerTemplate,
                    "textbox" => TextBoxTemplate,
                    "dropdown" => DropdownTemplate,
                    "checkbox" => CheckboxTemplate,
                    _ => TextBoxTemplate // fallback to textbox template
                };

                this.TraceMessage($"Selected template: {selectedTemplate?.GetType().Name ?? "null"}");
                return selectedTemplate ?? base.SelectTemplateCore(item, container);
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}