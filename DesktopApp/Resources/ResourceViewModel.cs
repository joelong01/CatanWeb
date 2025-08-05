using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security;

using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Models
{
    /// <summary>
    /// Represents a view model for a resource counter, including the count and resource type.
    /// </summary>
    public partial class ResourceCounterViewModel(int count, ResourceType resource) : ObservableObject
    {
        /// <summary>
        /// Gets or sets the count of the resource.
        /// </summary>
        [ObservableProperty]
        public partial int Count { get; set; } = count;

        /// <summary>
        /// Gets or sets the type of the resource.
        /// </summary>
        [ObservableProperty]
        public partial ResourceType Resource { get; set; } = resource;

        ///<summary>
        /// set if the player owns the 2:1 harbor for this resource
        /// </summary>
        /// 
        [ObservableProperty]
        public partial Visibility HarborVisibility { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Gets the default instance of the ResourceCounterViewModel class.
        /// </summary>
        public static ResourceCounterViewModel Default { get; } = new ResourceCounterViewModel(0, ResourceType.None);

        /// <summary>
        /// Gets the orientation based on the count.
        /// </summary>
        /// <param name="count">The count of the resource.</param>
        /// <returns>The orientation of the resource counter.</returns>
        public CatanOrientation GetOrientation(int count)
        {
            return count > 0 ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
        }

        /// <summary>
        /// Gets the front image brush for the specified resource type.
        /// </summary>
        /// <param name="resourceCardType">The type of the resource card.</param>
        /// <returns>An ImageBrush representing the front image of the resource card.</returns>
        public ImageBrush FrontImage(ResourceType resourceCardType)
        {
            return BrushCache.ResourceCardImage(resourceCardType);
        }
    }
    /// <summary>
    /// Represents a view model for resources, converting a ResourceModel to an observable collection that can be bound in XAML.
    /// </summary>
    public partial class ResourcesViewModel(IList<ResourceType> trackedResourceList) : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the resource model.
        /// </summary>
        [ObservableProperty]
        public partial ResourcesModel ResourceModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of resource counters.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<ResourceCounterViewModel> ResourceCounters { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of selected resources.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<ResourceCounterViewModel> SelectedResources { get; set; } = new();

        /// <summary>
        /// The list of tracked resource types.
        /// </summary>
        private IList<ResourceType> TrackedResourceList { get; } = trackedResourceList;

        /// <summary>
        /// Handles the selection changed event for resources.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        public void Resources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is GridView gridView)
            {
                foreach (ResourceCounterViewModel model in e.AddedItems.Cast<ResourceCounterViewModel>())
                {
                    SelectedResources.Add(model);
                }
                foreach (ResourceCounterViewModel model in e.RemovedItems.Cast<ResourceCounterViewModel>())
                {
                    SelectedResources.Remove(model);
                }

                if (SelectedResources.Count > 3)
                {
                    var itemToRemove = SelectedResources[0];
                    SelectedResources.RemoveAt(0);

                    // Deselect the item in the GridView
                    gridView.SelectedItems.Remove(itemToRemove);
                }

                var queryList = SelectedResources.Select(x => x.Resource).ToList();
                Messenger.Send(new QueryResourcesMessage(queryList));
            }
        }

        /// <summary>
        /// Updates the view model to represent the new data when the underlying ResourceModel changes.
        /// </summary>
        /// <param name="oldValue">The old ResourceModel value.</param>
        /// <param name="newValue">The new ResourceModel value.</param>
        partial void OnResourceModelChanging(ResourcesModel oldValue, ResourcesModel newValue)
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
