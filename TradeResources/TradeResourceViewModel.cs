using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class TradeResourceViewModel : ObservableObject
    {
        [ObservableProperty]
        private TradeResourcesModel resourceModel = new TradeResourcesModel();

    }
}
