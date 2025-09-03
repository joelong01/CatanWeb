using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;
using Catan3.GameService.Services;
using Microsoft.Extensions.Logging;

namespace Catan3.GameService.Utility
{
    /// <summary>
    /// Serializable version of the log for storage
    /// </summary>
    public class SerializableLog
    {
        public List<string> DoneStack { get; set; } = [];
        public List<string> RedoStack { get; set; } = [];
        public GameType GameType { get; set; } = GameType.Regular;
        public int DoneCount { get; set; } = 0;
        public int RedoCount { get; set; } = 0;
    }

    public interface ILog
    {
        GameType GameType { get; set; }
        int DoneCount { get; }
        int RedoCount { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        SerializableLog GetSerializableLog();
        void Done(GameModel model);
        GameModel? Undo();
        GameModel? Redo();
    }

    public class Log<T> : ILog
    {
        private IPersistenceService? PersistService { get; set; }
        public string FilePath { get; private set; }
        private ObservableCollection<T> DoneStack { get; set; } = [];
        private ObservableCollection<T> RedoStack { get; set; } = [];
        public GameType GameType { get; set; } = GameType.Regular;
        private readonly ILogger? _logger;

        [JsonConstructor]
        public Log(IPersistenceService? PersistenceService, string localSaveFile)
        {
            PersistService = PersistenceService;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            FilePath = localSaveFile;
            _logger = null; // No logger for JSON constructor
        }

        public Log(IPersistenceService? PersistenceService, string localSaveFile, ILogger? logger = null)
        {
            PersistService = PersistenceService;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            FilePath = localSaveFile;
            _logger = logger;
        }

        /// <summary>
        /// Constructor for loading existing GameModel from LoadGameModelMessage.
        /// Handles deserialization, validation, file path generation, and initialization.
        /// If message.IsTest is true, no file path is set (no saving). If false, uses GameModel.SaveFileName() for naming.
        /// </summary>
        public Log(IPersistenceService? PersistenceService, LoadGameModelMessage message, ILogger? logger = null)
        {
            PersistService = PersistenceService;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            _logger = logger;
            
            // Deserialize the GameModel from the message
            var gameModel = SerializationHelper.JsonDeserialize<GameModel>(message.GameModelJson)
                ?? throw new InvalidOperationException("Failed to deserialize GameModel from LoadGameModelMessage JSON");
            
            // Validate the deserialized GameModel
            gameModel.Validate();
            
            // Set file path based on test mode
            if (message.IsTest)
            {
                FilePath = string.Empty; // No saving for tests
            }
            else
            {
                // Use GameModel extension method for consistent naming
                FilePath = Path.Combine(Path.GetTempPath(), "Catan3Games", gameModel.SaveFileName());
            }
            
            // Initialize with the provided GameModel
            var json = SerializationHelper.JsonSerialize(gameModel);
            var data = (T)(object)json;
            DoneStack.Add(data);
            GameType = gameModel.GameType;
        }

        /// <summary>
        /// Constructor for loading existing GameModel with proper file path generation.
        /// If isTest is true, no file path is set (no saving). If false, uses GameModel.SaveFileName() for naming.
        /// </summary>
        public Log(IPersistenceService? PersistenceService, GameModel gameModel, bool isTest, ILogger? logger = null)
        {
            PersistService = PersistenceService;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            _logger = logger;
            
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
            var json = SerializationHelper.JsonSerialize(gameModel);
            var data = (T)(object)json;
            DoneStack.Add(data);
            GameType = gameModel.GameType;
        }

        /// <summary>
        /// Constructor for loading from compressed game data (SerializableLog).
        /// Creates a fully initialized Log with Done and Redo stacks from the SerializableLog.
        /// </summary>
        public Log(IPersistenceService? PersistenceService, Catan3.Shared.Interfaces.SerializableLog serializableLog, ILogger? logger = null)
        {
            PersistService = PersistenceService;
            DoneStack.CollectionChanged += DoneStack_ListChanged;
            RedoStack.CollectionChanged += RedoStack_ListChanged;
            _logger = logger;
            
            // Set file path (empty for loaded games - they manage their own persistence)
            FilePath = string.Empty;
            
            // Restore Done and Redo stacks from the SerializableLog
            foreach (var item in serializableLog.DoneStack)
            {
                var data = (T)(object)item;
                DoneStack.Add(data);
            }
            
            foreach (var item in serializableLog.RedoStack)
            {
                var data = (T)(object)item;
                RedoStack.Add(data);
            }
            
            // Restore other properties
            GameType = serializableLog.GameType;
        }

        private void UpdateFlags()
        {
            CanUndo = DoneStack.Count > 1;
            CanRedo = RedoStack.Count > 0;
        }

        public int DoneCount => DoneStack.Count;
        public int RedoCount => RedoStack.Count;

        // Fix partial properties - make them regular properties
        public bool CanUndo { get; private set; } = false;
        public bool CanRedo { get; private set; } = false;
        
        private void RedoStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null and ObservableCollection<T> list)
            {
                CanRedo = list.Count > 0;
            }
        }

        private void DoneStack_ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not null and ObservableCollection<T> list)
            {
                CanUndo = list.Count > 1; // don't undo past the start
            }
        }

        public void Done(GameModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model), "Provided GameModel cannot be null.");
            
            // Simplified implementation for now
            var json = SerializationHelper.JsonSerialize(model);
            var data = (T)(object)json;
            DoneStack.Add(data);
            RedoStack.Clear();
        }

        public GameModel? Undo()
        {
            if (!CanUndo) return null;

            try
            {
                var currentState = DoneStack[^1];
                DoneStack.RemoveAt(DoneStack.Count - 1);
                RedoStack.Add(currentState);

                var previousState = DoneStack[^1];
                var json = (string)(object)previousState!;
                return SerializationHelper.JsonDeserialize<GameModel>(json);
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError(ex, "[Trace] Undo operation failed: {Message}", ex.Message);
                }
                else
                {
                    // Fallback for cases where logger is not available
                    System.Diagnostics.Trace.WriteLine($"Undo operation failed: {ex.Message}");
                }
                return null;
            }
        }

        public GameModel? Redo()
        {
            if (!CanRedo) return null;

            try
            {
                var redoState = RedoStack[^1];
                RedoStack.RemoveAt(RedoStack.Count - 1);
                DoneStack.Add(redoState);

                var json = (string)(object)redoState!;
                return SerializationHelper.JsonDeserialize<GameModel>(json);
            }
            catch (Exception ex)
            {
                // Note: Consider injecting ILogger for better logging
                // For now, using simple approach to avoid changing constructor
                System.Diagnostics.Trace.WriteLine($"Redo operation failed: {ex.Message}");
                return null;
            }
        }

        public GameModel CurrentState()
        {
            if (DoneStack.Count == 0)
                throw new InvalidOperationException("DoneStack is empty, no data to retrieve.");

            var data = DoneStack[^1];
            var json = (string)(object)data!;
            return SerializationHelper.JsonDeserialize<GameModel>(json) 
                ?? throw new InvalidOperationException("Failed to convert the top of DoneStack to GameModel.");
        }

        public GameModel CopyCurrent()
        {
            if (DoneStack.Count == 0)
                throw new InvalidOperationException("DoneStack is empty, no data to retrieve.");

            var data = DoneStack[^1];
            var json = (string)(object)data!;
            return SerializationHelper.JsonDeserialize<GameModel>(json) 
                ?? throw new InvalidOperationException("Unable to deserialize GameModel");
        }

        public SerializableLog GetSerializableLog()
        {
            var log = new SerializableLog();
            
            // Convert stack to JSON strings
            for (int i = DoneStack.Count - 1; i >= 0; i--)
            {
                var json = (string)(object)DoneStack[i]!;
                log.DoneStack.Add(json);
            }

            for (int i = RedoStack.Count - 1; i >= 0; i--)
            {
                var json = (string)(object)RedoStack[i]!;
                log.RedoStack.Add(json);
            }

            log.GameType = GameType;
            log.DoneCount = DoneStack.Count;
            log.RedoCount = RedoStack.Count;
            return log;
        }

        public static Log<T> FromSerializableLog(SerializableLog sLog, IPersistenceService PersistenceService, string filePath)
        {
            var log = new Log<T>(PersistenceService, filePath);
            
            for (int i = sLog.DoneStack.Count - 1; i >= 0; i--)
            {
                var json = sLog.DoneStack[i];
                var data = (T)(object)json;
                log.DoneStack.Add(data);
            }
            
            for (int i = sLog.RedoStack.Count - 1; i >= 0; i--)
            {
                var json = sLog.RedoStack[i];
                var data = (T)(object)json;
                log.RedoStack.Add(data);
            }
            
            log.GameType = sLog.GameType;
            return log;
        }

        public static Log<T> FromCompressedString(string compressedBase64, IPersistenceService PersistenceService)
        {
            var compressedBytes = Convert.FromBase64String(compressedBase64);
            var json = SerializationHelper.Decompress(compressedBytes);
            var serializableLog = SerializationHelper.JsonDeserialize<SerializableLog>(json)
                ?? throw new InvalidOperationException("Failed to deserialize compressed log data");

            return FromSerializableLog(serializableLog, PersistenceService, string.Empty);
        }

        public async Task SaveAsync()
        {
            if (PersistService is null) return;
            
            // Don't save if FilePath is empty (test mode)
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                var uncompressedLog = GetSerializableLog();
                var json = SerializationHelper.JsonSerialize(uncompressedLog);
                var compressedBytes = SerializationHelper.Compress(json);
                await PersistService.SaveAsync(FilePath, compressedBytes);
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError(ex, "[Trace] Failed SaveAs: {Message}", ex.Message);
                }
                else
                {
                    // Fallback for cases where logger is not available
                    System.Diagnostics.Trace.WriteLine($"Failed SaveAs: {ex.Message}");
                }
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

    public static class SerializationHelper
    {
        public static string JsonSerialize<T>(T obj)
        {
            return JsonHelper.Serialize(obj);
        }

        public static T? JsonDeserialize<T>(string json)
        {
            return JsonHelper.Deserialize<T>(json);
        }

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