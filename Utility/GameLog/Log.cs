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
using MessagePack;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Catan3.Utility

{
    [MessagePackObject]
    public class SerializableLog
    {
        [Key(0)]
        public List<string> DoneStack { get; set; } = [];

        [Key(1)]
        public List<string> RedoStack { get; set; } = [];

        [Key(2)]
        public GameType GameType { get; set; } = GameType.Regular;

        [Key(3)]
        public int DoneCount { get; set; } = 0;
        [Key(4)]
        public int RedoCount { get; set; } = 0;



    }

    public partial class Log : ObservableObject
    {
     
        public ObservableCollection<string> DoneStack { get; set; } = [];
       
        public ObservableCollection<string> RedoStack { get; set; } = [];

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
            var log =  new SerializableLog();
            log.DoneStack.AddRange(DoneStack);
            log.RedoStack.AddRange(RedoStack);
            log.GameType = GameType;
            log.DoneCount = DoneStack.Count;
            log.RedoCount = RedoStack.Count;
            return log;
        }

        public void SetLog(SerializableLog log)
        {
            DoneStack.Clear();
            RedoStack.Clear();
            DoneStack.AddRange(log.DoneStack);
            RedoStack.AddRange(log.RedoStack);
            GameType = log.GameType;
        }

        private void RedoStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<string> list)
            {
                this.CanRedo = list.Count > 0;
                this.TraceMessage($"Redo Depth {list.Count}");
            }
        }

        private void DoneStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<string> list)
            {
                this.CanUndo = list.Count > 1; // don't undo past the start
                this.TraceMessage($"Done Depth {list.Count}");
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
            DoneStack.Push(model.Serialize());
            RedoStack.Clear();
        }
        /// <summary>
        /// Performs an undo operation by restoring the state from the undo stack.
        /// The current state is in the Done stack
        /// we need to pop the current state and push it onto the Redo stack
        /// then we need to Peek at the Undone stack and make that the current state
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

                var previousState = DoneStack.Peek();
                var model = GameModel.Deserialize(previousState) ?? throw new InvalidOperationException("Failed to deserialize the undo state.");
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
                var redoState = RedoStack.Pop();

                DoneStack.Push(redoState);

                var model = GameModel.Deserialize(redoState) ?? throw new InvalidOperationException("Failed to deserialize the redo state.");
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

        public string Peek()
        {
            return DoneStack.Peek();
        }

        public string Serialize()
        {

            return JsonSerializer.Serialize(this, _options);
        }

        public static Log? FromJson(string json)
        {

            return JsonSerializer.Deserialize<Log>(json, _options);
        }

        private static JsonSerializerOptions _options = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new LogConverter() },
            WriteIndented = true
        };

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

    public class LogConverter : JsonConverter<Log>
    {
        public override Log Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Log log = new Log();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();
                    switch (propName)
                    {
                        case "DoneStack":
                            log.DoneStack = JsonSerializer.Deserialize<ObservableCollection<string>>(ref reader, options) ?? throw new Exception("Invalid Json");
                            break;
                        case "RedoStack":
                            log.RedoStack = JsonSerializer.Deserialize<ObservableCollection<string>>(ref reader, options) ?? throw new Exception("Invalid Json"); ;
                            break;
                        case "GameType":
                            log.GameType = ( GameType )JsonSerializer.Deserialize<int>(ref reader, options);
                            break;
                    }
                }
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }
            }
            return log;
        }

        public override void Write(Utf8JsonWriter writer, Log value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("DoneStack");
            JsonSerializer.Serialize(writer, value.DoneStack, options);

            writer.WritePropertyName("RedoStack");
            JsonSerializer.Serialize(writer, value.RedoStack, options);

            writer.WritePropertyName("GameType");
            writer.WriteNumberValue(( int )value.GameType);

            writer.WriteEndObject();
        }
    }

    public class SerializationHelper
    {
        public static byte[] Pack<T>(T obj)
        {
            return MessagePackSerializer.Serialize(obj);
        }

        public static T Unpack<T>(byte[] data)
        {
            return MessagePackSerializer.Deserialize<T>(data);
        }

        public static string PackToJson<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public static T? UnpackFromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        private static JsonSerializerOptions _options = new()
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


