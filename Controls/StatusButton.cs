using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Catan3.Controls
{
    /// <summary>
    /// A custom Button that exposes a ModelId property through UI automation
    /// for reliable access to game state data in automated tests
    /// </summary>
    public class StatusButton : Button
    {
        public string? ModelId
        {
            get => (string?)GetValue(ModelIdProperty);
            set => SetValue(ModelIdProperty, value);
        }

        public static readonly DependencyProperty ModelIdProperty =
            DependencyProperty.Register(nameof(ModelId), typeof(string), typeof(StatusButton), new PropertyMetadata(null));

        protected override AutomationPeer OnCreateAutomationPeer()
            => new StatusButtonAutomationPeer(this);
    }
}
