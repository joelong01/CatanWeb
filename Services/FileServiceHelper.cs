using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Storage;
using WinUIEx;

namespace Catan3.Services
{
    /// <summary>
    /// Service helper for handling file operations using MVVM Toolkit's WeakReferenceMessenger.
    /// </summary>
    public class FileServiceHelper : ObservableRecipient
    {
        /// <summary>
        /// Asynchronously gets a file with the specified filters.
        /// </summary>
        /// <param name="filters">A list of file type filters to apply.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the selected StorageFile.</returns>
        public async Task<StorageFile?> GetFileAsync(WindowEx parent, IList<string> filters)
        {
            var tcs = new TaskCompletionSource<StorageFile?>();

            void MessageHandler(object recipient, OpenFileResponseMessage message)
            {
                tcs.SetResult(message.File);
                WeakReferenceMessenger.Default.Unregister<OpenFileResponseMessage>(this);
            }

            WeakReferenceMessenger.Default.Register<OpenFileResponseMessage>(this, MessageHandler);

            // Send the request message
            WeakReferenceMessenger.Default.Send(new OpenFileRequestMessage(parent, filters));

            return await tcs.Task;
        }
    }
}
