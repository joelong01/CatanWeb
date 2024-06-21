using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    public partial class QueryResourceModel : ObservableRecipient
    {

        [ObservableProperty]
        ResourceType _resourceType;
        [ObservableProperty]
        ImageBrush _background;
        [ObservableProperty]
        int  _count=0;

        public QueryResourceModel(ResourceType resourceType)
        {
            _resourceType = resourceType;
            _background = BrushCache.ResourceCardImage(resourceType);

        }


    }

    public partial class QueryBuilderModel : ObservableRecipient
    {

        [ObservableProperty]
        private ObservableCollection<QueryResourceModel> _queryResources = [new(ResourceType.Ore), new(ResourceType.Wheat), new(ResourceType.Sheep), new (ResourceType.Wood), new (ResourceType.Brick)];

        [ObservableProperty]
        private ObservableCollection<QueryResourceModel> _selectedResources = [];

      

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
