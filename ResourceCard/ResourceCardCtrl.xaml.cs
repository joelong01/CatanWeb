using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Catan3.Models;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class ResourceCardCtrl : UserControl
    {
        public ResourceCardCtrl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(ResourceCardModel), typeof(ResourceCardCtrl), new PropertyMetadata(new ResourceCardModel()));
        public ResourceCardModel ViewModel
        {
            get => ( ResourceCardModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
  

        private ImageBrush BIND_FrontImage(ResourceCardType resourceCardType)
        {
            this.TraceMessage($"Resource: {resourceCardType}");
            string key = $"ResourceCard.{resourceCardType}";
            var result =  ( ImageBrush )Application.Current.Resources[key];
            Debug.Assert(result is not null);
            return result;
        }

      
    }
}
