using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.Extensions;
using SerializableLog = Catan3.Shared.Interfaces.SerializableLog;
namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Represents a recorded action for test scenario creation.
    /// Simplified version that doesn't depend on test project types.
    /// </summary>
    public class RecordedAction
    {
        public string Type { get; set; } = string.Empty;
        public string? PlayerId { get; set; }
        public string? ExpectedState { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = [];
        public string? Description { get; set; }
    }

    public partial class Log<T> : IGameLog
    {
        private IPersistenceService? PersistService { get; set; }
        private readonly ICatanDebugTrace? _logger;
        public string FilePath { get; private set; }
        private ObservableCollection<T> DoneStack { get; set; } = [];
        private ObservableCollection<T> RedoStack { get; set; } = [];
        public GameType GameType { get; set; } = GameType.Regular;

        /// <summary>
        /// When true, the log will skip saving to prevent overwriting test files
        /// </summary>
        public bool InTestMode { get; set; } = false;

        /// <summary>
        /// Save coalescing: rapid calls to RequestSave() are coalesced so only
        /// the latest state is persisted. This prevents O(N) serialize+compress
        /// from running on every action in a fast sequence.
        /// </summary>
        private int _saveRequested = 0; // 0 = no pending save, 1 = save requested
        private int _saveRunning = 0;   // 0 = idle, 1 = save in progress

        /// <summary>
        /// Gets or sets whether the log is currently active for tracking operations.
        /// </summary>
        public bool IsActive { get; set; } = true;

        [JsonConstructor]
        public Log(IPersistenceService? PersistenceService, string localSaveFile)
        {
            PersistService = PersistenceService;
            _logger = null; // No logger for JSON constructor
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            FilePath = localSaveFile;

            // Automatically enable test mode for .catan_test files to prevent overwriting
            if (localSaveFile.EndsWith(".catan_test", StringComparison.OrdinalIgnoreCase))
            {
                InTestMode = true;
                _logger?.TraceMessage($"Trace constructor: Test mode ENABLED for file: {localSaveFile}");
            }
            else
            {
                _logger?.TraceMessage($"Trace constructor: Test mode disabled for file: {localSaveFile}");
            }
        }

        public Log(IPersistenceService? PersistenceService, string localSaveFile, ICatanDebugTrace? logger = null)
        {
            PersistService = PersistenceService;
            _logger = logger;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            FilePath = localSaveFile;

            // Automatically enable test mode for .catan_test files to prevent overwriting
            if (localSaveFile.EndsWith(".catan_test", StringComparison.OrdinalIgnoreCase))
            {
                InTestMode = true;
                _logger?.TraceMessage($"Trace constructor: Test mode ENABLED for file: {localSaveFile}");
            }
            else
            {
                _logger?.TraceMessage($"Trace constructor: Test mode disabled for file: {localSaveFile}");
            }
        }

        /// <summary>
        /// Constructor for loading existing GameModel with proper file path generation.
        /// If isTest is true, no file path is set (no saving). If false, uses GameModel.SaveFileName() for naming.
        /// </summary>
        public Log(IPersistenceService? PersistenceService, GameModel gameModel, bool isTest, ICatanDebugTrace? logger = null)
        {
            PersistService = PersistenceService;
            _logger = logger;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;

            // Set file path based on test mode
            if (isTest)
            {
                FilePath = string.Empty; // No saving for tests
            }
            else
            {
                // Use GameModel extension method for consistent naming
                FilePath = Path.Combine(Path.GetTempPath(), "Catan3Games", gameModel.SaveFileName());
            }

            // Initialize with the provided GameModel
            var json = JsonHelper.Serialize(gameModel);
            var data = (T)(object)json;
            DoneStack.Add(data);
            GameType = gameModel.GameType;
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }


        public int DoneCount => DoneStack.Count;
        public int RedoCount => RedoStack.Count;
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
                return CopyGameModel(model);
            string? json = null;
            // Handle compressed data.
            if (data is byte[] compressedData)
            {
                try
                {
                    json = JsonHelper.Decompress(compressedData);
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
                    return JsonSerializer.Deserialize<GameModel>(json, JsonHelper.StandardOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Failed to deserialize JSON to GameModel.", ex);
                }
            }
            // Throw an exception if none of the expected types are provided.
            throw new InvalidOperationException($"Unsupported type of T: {data?.GetType()}");
        }
        private static GameModel CopyGameModel(GameModel model)
        {
            string json = JsonSerializer.Serialize(model, JsonHelper.StandardOptions) ?? throw new Exception("GameModel must serialize!)");
            return JsonSerializer.Deserialize<GameModel>(json, JsonHelper.StandardOptions) ?? throw new Exception("GameModel must Deserialize!");
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
            string? json = JsonSerializer.Serialize(model) ?? throw new InvalidOperationException("Unable to serialize GameModel.");
            if (type == typeof(string)) return (T)(object)json;
            if (type == typeof(byte[]))
            {
                byte[] compressedData = JsonHelper.Compress(json);
                return (T)(object)compressedData;
            }
            if (type == typeof(GameModel))
            {
                object? o = JsonSerializer.Deserialize<GameModel>(json);
                Debug.Assert(o is not null);
                return (T)(object)o;
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
            if (type == typeof(string)) return (string)(object)data;
            // Handle serialization for GameModel instances
            if (type == typeof(GameModel))
            {
                // Cast is necessary to ensure the serializer accesses GameModel properties correctly
                return JsonSerializer.Serialize((GameModel)(object)data) ?? throw new InvalidOperationException("Unable to serialize GameModel.");
            }
            // Decompress byte array to JSON string assuming the byte array is compressed JSON
            if (data is byte[] byte_array)
            {
                return JsonHelper.Decompress(byte_array);
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
                return (T)(object)json;
            // Deserialize JSON into a GameModel if T is GameModel
            if (type == typeof(GameModel))
            {
                var deserialized = JsonHelper.Deserialize<GameModel>(json) ?? throw new InvalidOperationException("Unable to Deserialize GameModel.");
                return (T)(object)deserialized;
            }
            // Compress JSON into a byte array if T is byte[]
            if (type == typeof(byte[]))
            {
                byte[] compressed = JsonHelper.Compress(json);
                return (T)(object)compressed;
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
        /// Rehydrates a SerializableLog into a Trace<T> instance, converting serialized JSON strings 
        /// back into their original data types and preserving the LIFO order of operations as 
        /// represented in the DoneStack and RedoStack.
        /// </summary>
        /// <param name="sLog">The SerializableLog instance to convert.</param>
        /// <returns>A new Trace<T> instance populated with the data from the SerializableLog's stacks and game type.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the JSON deserialization fails or if the JSON format is not compatible with type T.</exception>
        public static Log<T> FromSerializableLog(SerializableLog sLog, IPersistenceService PersistenceService, string filePath)
        {
            var log = new Log<T>(PersistenceService, filePath);
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

        public static Log<T> FromCompressedString(string compressedBase64, IPersistenceService PersistenceService)
        {
            var compressedBytes = Convert.FromBase64String(compressedBase64);
            var json = JsonHelper.Decompress(compressedBytes);
            var serializableLog = JsonHelper.Deserialize<SerializableLog>(json)
                ?? throw new InvalidOperationException("Failed to deserialize compressed log data");

            return FromSerializableLog(serializableLog, PersistenceService, string.Empty);
        }

        /// <summary>
        /// Restores the log state from a serializable log structure.
        /// Required by IGameLog interface.
        /// </summary>
        /// <param name="serializableLog">The serializable log to restore from</param>
        /// <returns>The current game model after loading the log</returns>
        public GameModel LoadFromSerializableLog(SerializableLog serializableLog)
        {
            // Clear existing stacks
            DoneStack.Clear();
            RedoStack.Clear();

            // Load Done stack
            for (int i = serializableLog.DoneStack.Count - 1; i >= 0; i--)
            {
                var json = serializableLog.DoneStack[i];
                var data = ToT(json);
                DoneStack.Add(data);
            }

            // Load Redo stack  
            for (int i = serializableLog.RedoStack.Count - 1; i >= 0; i--)
            {
                var json = serializableLog.RedoStack[i];
                var data = ToT(json);
                RedoStack.Add(data);
            }

            // Restore game type
            GameType = serializableLog.GameType;

            // Return current state
            return CurrentState();
        }
        /// <summary>
        ///     creates a Trace<T> from a GameModel, serializing the GameModel to JSON and adding it to the DoneStack.
        ///     effectively loads the game from a GameModel. Used primarily in testing.
        /// </summary>
        /// <param name="gameModel"></param>
        /// <param name="myPersistenceService"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>

        internal static Log<T> FromGameModel(GameModel gameModel, IPersistenceService PersistenceService, string filePath)
        {
            var log = new Log<T>(PersistenceService, filePath);
            var json = JsonHelper.Serialize(gameModel) ?? throw new InvalidOperationException("Unable to Serialize GameModel.");
            log.GameType = log.GameType;  // Assign game type from SerializableLog
            log.DoneStack.Add(ToT(json)); // Add the GameModel as a JSON string to DoneStack
            return log;
        }
        private void RedoStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null and ObservableCollection<T> list)
            {
                this.CanRedo = list.Count > 0;
                //  _logger?.TraceMessage($"Redo Depth {list.Count} size={GetStackSize(list)}");
            }
        }
        private void DoneStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null and ObservableCollection<T> list)
            {
                this.CanUndo = list.Count > 1; // don't undo past the start
                                               // _logger?.TraceMessage($"Done Depth {list.Count}  size={GetStackSize(list)}");
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
        /// Initializes the log with an existing GameModel without any modifications.
        /// Used during game loading to preserve the original GameModel state.
        /// This method puts the GameModel directly into the DoneStack without processing.
        /// </summary>
        /// <param name="gameModel">The GameModel to initialize the log with</param>
        public void InitializeWithGameModel(GameModel gameModel)
        {
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel), "Provided GameModel cannot be null.");

            // Clear any existing state
            DoneStack.Clear();
            RedoStack.Clear();

            // Put the original GameModel into the DoneStack without modification
            T val;
            try
            {
                val = FromGameModel(gameModel);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to serialize the GameModel during initialization.", ex);
            }

            DoneStack.Push(val);

            // Set the GameType from the loaded model
            GameType = gameModel.GameType;
        }
        /// <summary>
        /// Performs an undo operation by restoring the state immediately preceding the current state
        /// This is achieved by moving the current state to the RedoStack and applying the previous state to the given viewModel.
        /// </summary>
        /// <returns>The GameModel representing the current state, null if Undo cannot be done.</returns>
        public GameModel? Undo()
        {
            if (!CanUndo)
                return null;
            try
            {
                // Move the current state to the RedoStack
                var currentState = DoneStack.Pop();
                RedoStack.Push(currentState);
                // Retrieve and deserialize the previous state
                var previousState = DoneStack.Peek();
                var newGameModel = ToGameModel(previousState) ?? throw new InvalidOperationException("Failed to deserialize the undo state.");
                return newGameModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo operation failed: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Restores the game state from the redo stack, pushing the current state onto the undo stack.
        /// This method pops the top state from the RedoStack, pushes the current state back to the DoneStack, and
        /// return that game model.
        /// </summary>
        /// <returns>True if the redo operation was successful; false otherwise.</returns>
        public GameModel? Redo()
        {
            if (!CanRedo)  // Check if there is a state to redo
                return null;
            try
            {
                // Pop the top state from the RedoStack and push it to the DoneStack
                var redoState = RedoStack.Pop();
                DoneStack.Push(redoState);
                // Deserialize the redo state and apply it to the viewModel
                var model = ToGameModel(redoState) ?? throw new InvalidOperationException("Failed to deserialize the redo state.");
                return model;
            }
            catch (Exception ex)
            {
                // Trace the error and return false indicating failure
                Debug.WriteLine($"Redo operation failed: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        ///     Updated via notification when the UndoStack changes
        /// </summary>
        [JsonIgnore]
        public bool CanUndo { get; private set; } = false;
        /// <summary>
        ///  Updated via notification when the RedoStack changes
        /// </summary>
        [JsonIgnore]
        public bool CanRedo { get; private set; } = false;
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
        public GameModel CopyCurrent()
        {
            if (DoneStack.Count == 0)
                throw new InvalidOperationException("DoneStack is empty, no data to retrieve.");
            string json = String.Empty;
            if (DoneStack.Peek() is T data)
            {
                if (data is byte[] compressedData)
                {
                    try
                    {
                        json = JsonHelper.Decompress(compressedData);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to decompress data.", ex);
                    }
                }
                // Handle string data assumed to be JSON.
                if (data is string jsonString)
                    json = jsonString;
                if (data is GameModel gameModel)
                {
                    json = JsonHelper.Serialize(gameModel);
                }
                GameModel copy = JsonHelper.Deserialize<GameModel>(json) ?? throw new InvalidOperationException("Unabled to deserialize GameModel");
                return copy;
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

        /// <summary>
        ///     Calls the FileService.Save passing in the full serialized stack.
        ///     the name of the file that is saved owned by the FileService
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Requests a save. Coalesces rapid calls — if a save is already in progress,
        /// the request is noted and the save will run again after the current one completes
        /// with the latest state. This is the primary entry point for persistence.
        /// Called by GameStateMachine.LogGameModel() after each action.
        /// </summary>
        public void RequestSave()
        {
            if (PersistService is null || InTestMode) return;

            // Mark that a save is needed
            Interlocked.Exchange(ref _saveRequested, 1);

            // If no save is running, start one
            if (Interlocked.CompareExchange(ref _saveRunning, 1, 0) == 0)
            {
                _ = Task.Run(RunSaveLoopAsync);
            }
        }

        /// <summary>
        /// Background save loop. Runs as long as saves are requested.
        /// Each iteration saves the current state. If new requests arrive
        /// during a save, the loop runs again with the latest state.
        /// </summary>
        private async Task RunSaveLoopAsync()
        {
            try
            {
                while (Interlocked.CompareExchange(ref _saveRequested, 0, 1) == 1)
                {
                    await SaveAsync();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _saveRunning, 0);

                // Check if another request arrived while we were finishing
                if (_saveRequested == 1)
                {
                    if (Interlocked.CompareExchange(ref _saveRunning, 1, 0) == 0)
                    {
                        _ = Task.Run(RunSaveLoopAsync);
                    }
                }
            }
        }

        public async Task SaveAsync()
        {
            if (PersistService is null) return;

            if (InTestMode) return;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var uncompressedLog = GetSerializableLog();
                var getLogMs = sw.ElapsedMilliseconds;

                var json = JsonHelper.Serialize(uncompressedLog);
                var serializeMs = sw.ElapsedMilliseconds - getLogMs;

                var compressedBytes = JsonHelper.Compress(json);
                var compressMs = sw.ElapsedMilliseconds - getLogMs - serializeMs;

                var gameModel = CurrentState();
                await PersistService.SaveAsync(gameModel.GameId, compressedBytes);
                sw.Stop();

                var dbMs = sw.ElapsedMilliseconds - getLogMs - serializeMs - compressMs;
                _logger?.TraceMessage($"[PERF-SAVE] getLog={getLogMs}ms serialize={serializeMs}ms compress={compressMs}ms db={dbMs}ms total={sw.ElapsedMilliseconds}ms jsonSize={json.Length / 1024}kb compressed={compressedBytes.Length / 1024}kb turns={uncompressedLog.DoneCount}");
            }
            catch (Exception ex)
            {
                _logger?.TraceMessage($"Failed SaveAsync: {ex.Message}");
            }
        }


        public async Task SaveAsAsync(string filePath)
        {
            try
            {
                this.FilePath = filePath;
                await SaveAsync();
            }
            catch
            {

                throw;
            }
        }


    }
    public static class LogExtensions
    {
        public static T Peek<T>(this IList<T> collection)
        {
            return (T)collection[^1];
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
}
