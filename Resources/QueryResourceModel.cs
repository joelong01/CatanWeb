using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    public partial class QueryResourceModel : ObservableObject
    {
       
        [ObservableProperty]
        ResourceType _resourceType;
        [ObservableProperty]
        ImageBrush _background;

        public QueryResourceModel(ResourceType resourceType)
        {
            _resourceType = resourceType;
            _background = BrushCache.ResourceCardImage(resourceType);

        }


    }

    public partial class QueryBuilderModel : ObservableObject
    {

        [ObservableProperty]
        private ObservableCollection<QueryResourceModel> _queryResources = [new(ResourceType.Ore), new(ResourceType.Wheat), new(ResourceType.Sheep), new (ResourceType.Wood), new (ResourceType.Brick)];

        [ObservableProperty]
        private ObservableCollection<QueryResourceModel> _selectedResources = [];

        public void Resources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (QueryResourceModel model in e.AddedItems.Cast<QueryResourceModel>())
            {

                SelectedResources.Add(model);
            }
            foreach (QueryResourceModel model in e.RemovedItems.Cast<QueryResourceModel>())
            {

                SelectedResources.Remove(model);
            }
        }
    }
}
