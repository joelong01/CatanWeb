using System;

using Catan3.Models;
using Microsoft.UI.Xaml.Controls;

namespace Catan3.Controls
{
    public sealed class NewGameContentDialog : ContentDialog
    {
        public NewGameContentDialog(NewGameViewModel model) : base()
        {

            this.Title = "New Game";
            this.PrimaryButtonText = "Ok";
            this.SecondaryButtonText = "Cancel";
            this.DefaultButton = ContentDialogButton.Primary;



            // Load the custom content
            var content = new NewGameCtrl(model);

            this.Content = content;
            // Handle button clicks
           
        }

       
    }
}
