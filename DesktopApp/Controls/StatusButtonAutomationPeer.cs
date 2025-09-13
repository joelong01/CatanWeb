using Microsoft.UI.Xaml.Automation.Peers;

namespace Catan3.Controls
{
    /// <summary>
    /// Automation peer for StatusButton that exposes the ModelId property
    /// as ItemStatus for reliable UI automation access
    /// </summary>
    public class StatusButtonAutomationPeer : ButtonAutomationPeer
    {
        public StatusButtonAutomationPeer(StatusButton owner) : base(owner) { }
        
        private StatusButton Btn => (StatusButton)Owner;

        protected override string GetItemStatusCore()
            => Btn.ModelId ?? base.GetItemStatusCore();

        // Optional: enrich the Name to include status info for debugging
        protected override string GetNameCore()
        {
            var baseName = base.GetNameCore();
            var modelId = Btn.ModelId;
            
            if (!string.IsNullOrEmpty(modelId))
            {
                // Include a hint about the data length for debugging
                var dataLength = modelId.Length;
                return $"{baseName} [Data:{dataLength}]";
            }
            
            return baseName;
        }
    }
}
