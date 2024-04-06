

using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Contacts;

namespace Catan3.Models
{

  


    public partial class GameViewModel : ObservableRecipient
    {
        public GameViewModel()
        {
            // Register to receive the BuildingUpdateMessage
            
        }

       
    }
}
