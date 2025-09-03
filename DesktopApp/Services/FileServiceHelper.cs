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
        public async Task<StorageFile?> GetStorageFileAsync(WindowEx parent, IList<string> filters)
        {
            var tcs = new TaskCompletionSource<StorageFile?>();
            async void MessageHandler(object recipient, OpenFileResponseMessage message)
            {
                try
                {
                    if (message.FilePath != null)
                    {
                        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(message.FilePath);
                        tcs.SetResult(storageFile);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    tcs.SetResult(null);
                }
                finally
                {
                    WeakReferenceMessenger.Default.Unregister<OpenFileResponseMessage>(this);
                }
            }
            WeakReferenceMessenger.Default.Register<OpenFileResponseMessage>(this, MessageHandler);
            // Send the request message
            WeakReferenceMessenger.Default.Send(new OpenFileRequestMessage(parent, filters));
            return await tcs.Task;
        }
    }
}
