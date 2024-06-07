using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Models
{
    public partial class ResourceCounterViewModel(int count, ResourceType resource) : ObservableObject
    {
        [ObservableProperty]
        private int _count = count;
        [ObservableProperty]
        private ResourceType _resource = resource;
        public static ResourceCounterViewModel Default { get; } = new ResourceCounterViewModel(0, ResourceType.None);
        public CatanOrientation GetOrientation(int count)
        {
            return count > 0 ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
        }
        public ImageBrush FrontImage(ResourceType resourceCardType)
        {
            return BrushCache.ResourceCardImage(resourceCardType);
        }
    }
    /// <summary>
    ///     this class needs to convert a ResourceModel -- simple counters of ResourceTypes to an observable
    ///     collection that can be bound in XAML
    /// </summary>
    public partial class ResourcesViewModel(IList<ResourceType> trackedResourceList) : ObservableRecipient
    {
        [ObservableProperty]
        private ResourcesModel _resourceModel = new();
        // not an ObservableProperty 
        private IList<ResourceType> TrackedResourceList = trackedResourceList;
        [ObservableProperty]
        private ObservableCollection<ResourceCounterViewModel> _resourceCounters = [];
        /// <summary>
        ///     When the underlying GameModel changes, the ResourceModel updates. We go through and update
        ///     the ViewModel to represent the new data.  Note that we *do not* recreate the collection
        ///     because it makes the UI flash.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        partial void OnResourceModelChanging(ResourcesModel? oldValue, ResourcesModel newValue)
        {
            if (newValue is null)
            {
                Debug.Assert(newValue is not null);
            }
          
            Debug.Assert(TrackedResourceList.Count > 0);
            foreach (var resource in this.TrackedResourceList)
            {
                ResourceCounterViewModel? rcvm = ResourceCounters.FirstOrDefault(r => r.Resource == resource);
                if (rcvm is null)
                {
                    rcvm = new(newValue.CountForResource(resource), resource);
                    ResourceCounters.Add(rcvm);
                }
                rcvm.Count = newValue.CountForResource(resource);
               
            }
        }
       
    }
}
