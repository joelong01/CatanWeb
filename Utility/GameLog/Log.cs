using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Media.Capture;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Catan3.Utility

{
    /// <summary>
    ///     The System.Text.Serialize package has trouble composing with MVVM becuse of the Code behind strategy
    ///     Se can can convert to a SerializableLog and then Json serialize it.  In testing, compressing the JSON
    ///     of an individual GameModel reduces side by about 50%.  Compressing the full stack is a huge reduction -
    ///     50 GameModels are hundreds of K compressed, but only 5k compressed.
    /// </summary>
    //  
    public class SerializableLog
    {

        public List<string> DoneStack { get; set; } = [];
        public List<string> RedoStack { get; set; } = [];
        public GameType GameType { get; set; } = GameType.Regular;
        public int DoneCount { get; set; } = 0;
        public int RedoCount { get; set; } = 0;

    }

    public partial class Log<T> : ObservableObject
    {

        private ObservableCollection<T> DoneStack { get; set; } = [];

        private ObservableCollection<T> RedoStack { get; set; } = [];

        public GameType GameType { get; set; } = GameType.Regular;
        [JsonConstructor]
        public Log()
        {
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
        }
        public Log(GameType gameType) : this()
        {

            GameType = gameType;
        }

        public int DoneCount => DoneStack.Count;

        /// <summary>
        /// Retrieves a GameModel from the provided data, handling different types of input.
        /// If the input is already a GameModel, it returns it directly. If it is a byte byte_array, it assumes
        /// the byte byte_array is compressed JSON and decompresses it, then deserializes it into a GameModel.
        /// If the input is a JSON string, it deserializes it directly into a GameModel.
        /// </summary>
        /// <param name="data">The data input which can be of type GameModel, byte[], or string.</param>
        /// <returns>A GameModel instance deserialized from the provided data or directly returned if already a GameModel.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the input data type is not supported or if deserialization fails.</exception>

        private static GameModel? ToGameModel(T data)
        {
            // Directly return the data if it is already a GameModel.
            if (data is GameModel model)
                return model;

            string? json = null;

            // Handle compressed data.
            if (data is byte[] compressedData)
            {
                try
                {
                    json = SerializationHelper.Decompress(compressedData);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to decompress data.", ex);
                }
            }

            // Handle string data assumed to be JSON.
            if (data is string jsonString)
                json = jsonString;

            // Deserialize JSON to GameModel if possible.
            if (json is not null)
            {
                try
                {
                    return JsonSerializer.Deserialize<GameModel>(json);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Failed to deserialize JSON to GameModel.", ex);
                }
            }

            // Throw an exception if none of the expected types are provided.
            throw new InvalidOperationException($"Unsupported type of T: {data?.GetType()}");
        }

        /// <summary>
        /// Converts a GameModel instance into a specified type T. The type T can be a GameModel,
        /// a string (JSON representation), or a byte byte_array (compressed JSON). This method assumes
        /// that the necessary logic to convert to type T is correctly implemented based on the expected types.
        /// </summary>
        /// <param name="model">The GameModel instance to convert.</param>
        /// <returns>The GameModel converted to type T, which may be the model itself, its JSON string representation,
        /// or a compressed byte byte_array of its JSON representation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the conversion to type T is not possible or not implemented.</exception>
        private static T FromGameModel(GameModel model)
        {
            Type type = typeof(T);

            if (type == typeof(GameModel))
            {
                return ( T )( object )model;
            }
            string? json = JsonSerializer.Serialize(model) ?? throw new InvalidOperationException("Unable to serialize GameModel.");
            if (type == typeof(string)) return ( T )( object )json;
            if (type == typeof(byte[]))
            {
                byte[] compressedData = SerializationHelper.Compress(json);
                return ( T )( object )compressedData;
            }

            throw new InvalidOperationException($"Conversion from GameModel to type {typeof(T)} is not supported.");

        }

        /// <summary>
        /// Converts data of type T to its JSON string representation. Supports string, GameModel, and byte[] types.
        /// </summary>
        /// <param name="data">The data to be converted to JSON.</param>
        /// <returns>A JSON string representation of the input data.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the input data is null or if the data type is not supported for conversion.</exception>
        private static string ToJson(T data)
        {
            if (data is null) throw new InvalidOperationException("Data cannot be null.");

            Type type = typeof(T);
            // Return directly if the data is already a string
            if (type == typeof(string)) return ( string )( object )data;

            // Handle serialization for GameModel instances
            if (type == typeof(GameModel))
            {
                // Cast is necessary to ensure the serializer accesses GameModel properties correctly
                return JsonSerializer.Serialize(( GameModel )( object )data) ?? throw new InvalidOperationException("Unable to serialize GameModel.");
            }

            // Decompress byte array to JSON string assuming the byte array is compressed JSON
            if (data is byte[] byte_array)
            {
                return SerializationHelper.Decompress(byte_array);
            }

            // Throw an exception if the data type is not one of the expected types
            throw new InvalidOperationException("Unsupported type for JSON conversion.");
        }
        /// <summary>
        /// Converts a JSON string into a specified type T. This method handles three potential types for T:
        /// it returns the JSON string as is if T is string; deserializes the JSON into a GameModel if T is GameModel;
        /// and compresses the JSON string into a byte array if T is byte[]. This method provides a way to dynamically
        /// handle the conversion based on the type of T, using generic type constraints.
        /// </summary>
        /// <param name="json">The JSON string to be converted. The string should not be null and must be a valid JSON format.</param>
        /// <returns>
        /// The converted object of type T: as a plain string, a GameModel object, or a byte array, depending on T.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON string is null, or if the conversion fails (e.g., deserialization issues,
        /// or attempting to convert to an unsupported type).
        /// </exception>

        private static T ToT(string json)
        {
            if (json is null)
                throw new InvalidOperationException("Json cannot be null.");

            Type type = typeof(T);

            // Return directly if T is string
            if (type == typeof(string))
                return ( T )( object )json;

            // Deserialize JSON into a GameModel if T is GameModel
            if (type == typeof(GameModel))
            {
                var deserialized = SerializationHelper.JsonDeserialize<GameModel>(json) ?? throw new InvalidOperationException("Unable to Deserialize GameModel.");
                return ( T )( object )deserialized;
            }

            // Compress JSON into a byte array if T is byte[]
            if (type == typeof(byte[]))
            {
                byte[] compressed = SerializationHelper.Compress(json);
                return ( T )( object )compressed;
            }

            // Throw an exception if T is not one of the expected types
            throw new InvalidOperationException($"Unsupported type for JSON conversion: {type.Name}.");
        }


        /// <summary>
        /// Creates a serializable version of the current log, converting all entries in the Done and Redo stacks
        /// from their current types to JSON strings. This facilitates efficient compression and storage.
        /// </summary>
        /// <returns>A SerializableLog containing JSON representations of the original stack data.</returns>
        public SerializableLog GetSerializableLog()
        {
            var log = new SerializableLog();
            // Reverse the order of the DoneStack to maintain the LIFO order when serialized
            for (int i = DoneStack.Count - 1; i >= 0; i--)
            {
                var json = ToJson(DoneStack[i]);
                log.DoneStack.Add(json);
            }
            // Reverse the order of the RedoStack to maintain the LIFO order when serialized
            for (int i = RedoStack.Count - 1; i >= 0; i--)
            {
                var json = ToJson(RedoStack[i]);
                log.RedoStack.Add(json);
            }

            log.GameType = GameType;
            log.DoneCount = DoneStack.Count;
            log.RedoCount = RedoStack.Count;
            return log;
        }

        /// <summary>
        /// Rehydrates a SerializableLog into a Log<T> instance, converting serialized JSON strings 
        /// back into their original data types and preserving the LIFO order of operations as 
        /// represented in the DoneStack and RedoStack.
        /// </summary>
        /// <param name="sLog">The SerializableLog instance to convert.</param>
        /// <returns>A new Log<T> instance populated with the data from the SerializableLog's stacks and game type.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the JSON deserialization fails or if the JSON format is not compatible with type T.</exception>
        public static Log<T> FromSerializableLog(SerializableLog sLog)
        {
            var log = new Log<T>();
            for (int i = sLog.DoneStack.Count - 1; i >= 0; i--)
            {
                var json = sLog.DoneStack[i];
                var data = ToT(json);  // Deserialize and convert back to T
                log.DoneStack.Add(data);
            }
            for (int i = sLog.RedoStack.Count - 1; i >= 0; i--)
            {
                var json = sLog.RedoStack[i];
                var data = ToT(json);  // Deserialize and convert back to T
                log.RedoStack.Add(data);  // Corrected to add to RedoStack
            }

            log.GameType = sLog.GameType;  // Assign game type from SerializableLog

            return log;
        }


        private void RedoStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<T> list)
            {
                this.CanRedo = list.Count > 0;
                this.TraceMessage($"Redo Depth {list.Count} size={GetStackSize(list)}");
            }
        }

        private void DoneStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null && sender is ObservableCollection<T> list)
            {
                this.CanUndo = list.Count > 1; // don't undo past the start
                this.TraceMessage($"Done Depth {list.Count}  size={GetStackSize(list)}");
            }
        }

        /// <summary>
        /// Serializes the provided GameModel and pushes the serialized version onto the DoneStack.
        /// Also clears the RedoStack to prepare for new operations.
        /// </summary>
        /// <param name="model">The GameModel to serialize and add to the DoneStack.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided GameModel is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the serialization fails or if the data type is not supported.</exception>
        public void Done(GameModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model), "Provided GameModel cannot be null.");

            T val;
            try
            {
                val = FromGameModel(model);
            }
            catch (Exception ex)
            {
                // Consider logging the exception if appropriate
                throw new InvalidOperationException("Failed to serialize the GameModel.", ex);
            }

            DoneStack.Push(val);
            RedoStack.Clear();
        }

        /// <summary>
        /// Performs an undo operation by restoring the state immediately preceding the current state.
        /// This is achieved by moving the current state to the RedoStack and applying the previous state to the given viewModel.
        /// </summary>
        /// <param name="viewModel">The game view model containing the current game state, which will be updated to the previous state.</param>
        /// <returns>True if the undo operation was successful; false otherwise.</returns>
        public bool Undo(GameViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel), "ViewModel cannot be null.");

            if (!CanUndo)
                return false;

            try
            {
                // Move the current state to the RedoStack
                var currentState = DoneStack.Pop();
                RedoStack.Push(currentState);

                // Retrieve and deserialize the previous state
                var previousState = DoneStack.Peek();
                var model = ToGameModel(previousState) ?? throw new InvalidOperationException("Failed to deserialize the undo state.");

                // Apply the restored state
                viewModel.MergeGameModel(model);
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
        /// This method pops the top state from the RedoStack, applies it to the provided viewModel,
        /// and pushes the current state back to the DoneStack, effectively reversing a previous undo operation.
        /// </summary>
        /// <param name="viewModel">The game view model to which the state will be applied. This model is updated to reflect the restored state.</param>
        /// <returns>True if the redo operation was successful; false otherwise.</returns>
        public bool Redo(GameViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel), "ViewModel cannot be null.");

            if (!CanRedo)  // Check if there is a state to redo
                return false;

            try
            {
                // Pop the top state from the RedoStack and push it to the DoneStack
                var redoState = RedoStack.Pop();
                DoneStack.Push(redoState);

                // Deserialize the redo state and apply it to the viewModel
                var model = ToGameModel(redoState) ?? throw new InvalidOperationException("Failed to deserialize the redo state.");
                viewModel.MergeGameModel(model);  // Apply the restored state
                return true;
            }
            catch (Exception ex)
            {
                // Log the error and return false indicating failure
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
        /// Retrieves the current game state from the top of the DoneStack.
        /// </summary>
        /// <returns>The current GameModel representing the top of the DoneStack.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the DoneStack is empty or if the data at the top of the stack cannot be converted to a GameModel.</exception>
        public GameModel CurrentState()
        {
            if (DoneStack.Count == 0)
                throw new InvalidOperationException("DoneStack is empty, no data to retrieve.");

            if (DoneStack.Peek() is T data)
            {
                return ToGameModel(data) ?? throw new InvalidOperationException("Failed to convert the top of DoneStack to GameModel.");
            }

            // This code should theoretically never be reached because of the above check, but it's a safeguard.
            throw new InvalidOperationException("Unexpected type in DoneStack, cannot convert to GameModel");
        }


        public static int GetStackSize(IList<T> data)
        {
            // Calculate the total size for byte[] and string types using LINQ
            return data.Sum(item => item switch
            {
                byte[] bytes => bytes.Length,     // Sum lengths of byte arrays
                string str => str.Length,         // Sum lengths of strings (in characters, not bytes)
                _ => 0                            // If it's a GameModel or any unsupported type, contribute 0 to the sum
            });
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

        public static byte[] Compress(string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            using var memoryStream = new MemoryStream();
            using (var brotliStream = new BrotliStream(memoryStream, CompressionMode.Compress, true))
            {
                brotliStream.Write(buffer, 0, buffer.Length);
            }
            return memoryStream.ToArray();
        }

        public static string Decompress(byte[] data)
        {
            using var compressedStream = new MemoryStream(data);
            using var brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            brotliStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
    }
}


