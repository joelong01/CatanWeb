using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Security.Cryptography;
using System.Linq;


namespace Catan3.Utility

{
    public class SerializableLog
    {
   
        public List<string> DoneStack { get; set; } = [];


        public List<string> RedoStack { get; set; } = [];


        public GameType GameType { get; set; } = GameType.Regular;


        public int DoneCount { get; set; } = 0;
        public int RedoCount { get; set; } = 0;



    }

    public partial class Log : ObservableObject
    {
     
        public ObservableCollection<byte[]> DoneStack { get; set; } = [];
       
        public ObservableCollection<byte[]> RedoStack { get; set; } = [];

        public GameType GameType { get; set; } = GameType.Regular;
        public Log() { }
        public Log(GameType gameType)
        {
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            GameType = gameType;
        }

        public int DoneCount => DoneStack.Count;

        public SerializableLog GetSerializableLog()
        {
            var log = new SerializableLog();
            // Preserve the order: the earliest entry first as in a stack operation
            for (int i = DoneStack.Count - 1; i >= 0; i--)
            {
                var json = SerializationHelper.DecompressString(DoneStack[i]);
                log.DoneStack.Add(json);
            }
            for (int i = RedoStack.Count - 1; i >= 0; i--)
            {
                var json = SerializationHelper.DecompressString(RedoStack[i]);
                log.RedoStack.Add(json);
            }

            log.GameType = GameType;  
            return log;
        }

       



        private void RedoStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<byte[]> list)
            {
                this.CanRedo = list.Count > 0;
                this.TraceMessage($"Redo Depth {list.Count} size={DumpLogSize(list)}");
            }
        }

        private void DoneStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<byte[]> list)
            {
                this.CanUndo = list.Count > 1; // don't undo past the start
                this.TraceMessage($"Done Depth {list.Count}  size={DumpLogSize(list)}");
            }
        }

        /// <summary>
        ///     Serialize the model
        ///     put it on the DoneStack
        ///     clear the RedoStack
        /// </summary>
        /// <param name="model"></param>
        public void Done(GameModel model)
        {
            var json = model.Serialize();
            var compressed = SerializationHelper.CompressString(json);
            DoneStack.Push(compressed);
            RedoStack.Clear();
        }
        /// <summary>
        /// Performs an undo operation by restoring the state from the undo stack.
        /// The current state is in the Done stack
        /// we need to pop the current state and push it onto the Redo stack
        /// then we need to CurrentState at the Undone stack and make that the current state
        /// </summary>
        /// <param name="viewModel">The game view model containing the current game state.</param>
        /// <returns>true if the undo operation was successful; false otherwise.</returns>
        public bool Undo(GameViewModel viewModel)
        {
            if (!CanUndo)
                return false;

            try
            {


                var currentState = DoneStack.Pop();
                RedoStack.Push(currentState);

                var previousStateCompressed = DoneStack.Peek();
                this.TraceMessage($"compressed record: {previousStateCompressed.Length}");
                var previousJson = SerializationHelper.DecompressString(previousStateCompressed);
                var model = GameModel.Deserialize(previousJson) ?? throw new InvalidOperationException("Failed to deserialize the undo state.");
                viewModel.MergeGameModel(model);  // Apply the restored state
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo operation failed: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Restores the game state from the redo stack, pushing the current state onto the undo stack.
        /// </summary>
        /// <param name="viewModel">The game view model to which the state will be applied.</param>
        /// <returns>true if the redo operation was successful; false otherwise.</returns>
        public bool Redo(GameViewModel viewModel)
        {
            if (!CanRedo)  // More explicit than CanRedo for understanding
                return false;

            try
            {
                var redoStateCompressed = RedoStack.Pop();

                DoneStack.Push(redoStateCompressed);

                var json = SerializationHelper.DecompressString(redoStateCompressed);

                var model = GameModel.Deserialize(json) ?? throw new InvalidOperationException("Failed to deserialize the redo state.");
                viewModel.MergeGameModel(model);  // Apply the restored state
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Redo operation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Updating via notifcation when the UndoStack changes
        /// </summary>
        [JsonIgnore]
        [ObservableProperty]
        private bool _canUndo = false;

        /// <summary>
        ///  Updated via notification when the RedoStack changes
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private bool _canRedo = false;
        
        /// <summary>
        ///     Used when reading the log to find out what the current state should be
        /// </summary>
        /// <returns></returns>
        public GameModel CurrentState()
        {
            var compressedBytes =  DoneStack.Peek();
            var json = SerializationHelper.DecompressString(compressedBytes);
        
            var gameModel = SerializationHelper.JsonDeserialize<GameModel>(json) ?? throw new Exception("Invalid Game File");
            return gameModel;
        }

        public static int DumpLogSize(IList<byte[]> data)
        {
            var size = data.Sum( a => a.Length );
            size.TraceMessage($"Size: {size}");
            return size;
        }
    }

    public static class LogExtensions
    {
        public static T Peek<T>(this IList<T> collection)
        {
            return ( T )collection[^1];
        }

        public static T Pop<T>(this IList<T> collection)
        {
            T item = collection[^1];
            collection.RemoveAt(collection.Count - 1);
            return item;
        }

        public static void Push<T>(this IList<T> collection, T item)
        {
            collection.Add(item);
        }
    }

    public class SerializationHelper
    {
       
        public static string JsonSerialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, JsonOptions);
        }

        public static T? JsonDeserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        public static byte[] CompressString(string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            using var memoryStream = new MemoryStream();
            using (var brotliStream = new BrotliStream(memoryStream, CompressionMode.Compress, true))
            {
                brotliStream.Write(buffer, 0, buffer.Length);
            }
            return memoryStream.ToArray();
        }

        public static string DecompressString(byte[] data)
        {
            using var compressedStream = new MemoryStream(data);
            using var brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            brotliStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
    }
}


