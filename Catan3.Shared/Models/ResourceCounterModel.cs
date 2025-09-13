using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Model for tracking resource counts and types.
    /// Used for game balance calculations and resource management.
    /// </summary>
    public partial class ResourceCounterModel : ObservableObject
    {
        [ObservableProperty]
        public partial int Count { get; set; }

        [ObservableProperty]
        public partial ResourceType ResourceType { get; set; }

        public ResourceCounterModel() { }

        public ResourceCounterModel(int count, ResourceType resourceType)
        {
            Count = count;
            ResourceType = resourceType;
        }
    }
}