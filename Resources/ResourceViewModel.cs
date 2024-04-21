using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
namespace Catan3.Models
{


    public partial class ResourcesViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private ResourcesModel? _resourceModel;


        [ObservableProperty]
        private ObservableCollection<ResourceType> _trackedResourceTypes = [];

        public ResourcesViewModel()
        {
            Messenger.Register<TrackedResourceTypes>(this, (recipient, message) =>
            {
                this.TraceMessage($"message recieved");
                TrackedResourceTypes.AddRange(message.TrackedResources);
                Messenger.Unregister<TrackedResourceTypes>(this);
            });
        }

        public ObservableCollection<ResourceType> GetTrackedResources(bool trackGold)

        {
            if (trackGold && !TrackedResourceTypes.Contains(ResourceType.GoldMine))
            {
                TrackedResourceTypes.Add(ResourceType.GoldMine);
            }
            if (!trackGold && TrackedResourceTypes.Contains(ResourceType.GoldMine))
            {
                TrackedResourceTypes.Remove(ResourceType.GoldMine);
            }
            return TrackedResourceTypes;
        }


        public CatanOrientation Orientation(ResourcesModel? tr, ResourceType resource)
        {
            if (tr is null) return CatanOrientation.FaceDown;

            int count = tr.CountForResource(resource);
            return count > 0 ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
        }

        public string Count(ResourcesModel? tr, ResourceType resource)
        {
            if (tr is null) return "Null!!";

            int count =  tr.CountForResource(resource);
            if (resource == ResourceType.GoldMine)
            {
                this.TraceMessage($"gold={count}");
            }
            return count.ToString();

        }
    }

}
