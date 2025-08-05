using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    /// <summary>
    /// Represents a model for querying resources, including the resource type, background image, and count.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the QueryResourceModel class with the specified resource type.
    /// </remarks>
    /// <param name="resourceType">The type of the resource.</param>
    public partial class QueryResourceModel(ResourceType resourceType) : ObservableRecipient
    {

        /// <summary>
        /// Gets or sets the type of the resource.
        /// </summary>
        [ObservableProperty]
        public partial ResourceType ResourceType { get; set; } = resourceType;

        /// <summary>
        /// Gets or sets the background image brush for the resource.
        /// </summary>
        [ObservableProperty]
        public partial ImageBrush Background { get; set; } = BrushCache.ResourceCardImage(resourceType);

        /// <summary>
        /// Gets or sets the count of the resource.
        /// </summary>
        [ObservableProperty]
        public partial int Count { get; set; } = 0;
    }

    /// <summary>
    /// Represents a model for building resource queries, including collections of query resources and selected resources.
    /// </summary>
    public partial class QueryBuilderModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the collection of query resources.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<QueryResourceModel> QueryResources { get; set; } = new()
            {
                new(ResourceType.Ore),
                new(ResourceType.Wheat),
                new(ResourceType.Sheep),
                new(ResourceType.Wood),
                new(ResourceType.Brick)
            };

        /// <summary>
        /// Gets or sets the collection of selected resources.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<QueryResourceModel> SelectedResources { get; set; } = new();

        /// <summary>
        /// Handles the selection changed event for resources.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        public void Resources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is GridView gridView)
            {
                foreach (QueryResourceModel model in e.AddedItems.Cast<QueryResourceModel>())
                {
                    SelectedResources.Add(model);
                }
                foreach (QueryResourceModel model in e.RemovedItems.Cast<QueryResourceModel>())
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

                var queryList = SelectedResources.Select(x => x.ResourceType).ToList();
                Messenger.Send(new QueryResourcesMessage(queryList));
            }
        }
    }
}
