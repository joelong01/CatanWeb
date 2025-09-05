using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Catan3.Shared.Utility;

namespace Catan3.CLI.Commands
{
    /// <summary>
    /// Handles extracting GameModel from compressed .catan files.
    /// </summary>
    public class ExtractCommand
    {
        /// <summary>
        /// Extracts a specific GameModel by index from a .catan file.
        /// </summary>
        /// <param name="inputPath">Path to the input .catan file</param>
        /// <param name="outputPath">Path to the output JSON file or "stdout"</param>
        /// <param name="index">Index in DoneStack (0 = most recent)</param>
        public static async Task ExtractGameModelByIndexAsync(string inputPath, string outputPath, int index)
        {
            try
            {
                // Validate input file exists
                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException($"Input file not found: {inputPath}");
                }
                
                // Read and decompress the .catan file
                var compressedBytes = await File.ReadAllBytesAsync(inputPath);
                var decompressedJson = Decompress(compressedBytes);
                
                // Parse the decompressed data (this is a SerializableLog)
                using var document = JsonDocument.Parse(decompressedJson);
                var root = document.RootElement;
                
                // The .catan file contains a SerializableLog with doneStack
                if (!root.TryGetProperty("doneStack", out var doneStack))
                {
                    var properties = root.EnumerateObject().Select(p => p.Name).ToArray();
                    throw new InvalidOperationException($"No 'doneStack' property found. Available: {string.Join(", ", properties)}");
                }
                
                var doneStackArray = doneStack.EnumerateArray().ToArray();
                if (doneStackArray.Length == 0)
                {
                    throw new InvalidOperationException("DoneStack is empty - no GameModel to extract");
                }
                
                if (index >= doneStackArray.Length || index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range. DoneStack has {doneStackArray.Length} elements (0-{doneStackArray.Length - 1})");
                }
                
                // Get the GameModel at the specified index
                var gameModelJson = doneStackArray[index].GetString();
                if (string.IsNullOrEmpty(gameModelJson))
                {
                    throw new InvalidOperationException($"GameModel at index {index} is null or empty");
                }
                
                // Parse and format the GameModel JSON
                using var gameModelDoc = JsonDocument.Parse(gameModelJson);
                var formattedJson = JsonSerializer.Serialize(gameModelDoc.RootElement, JsonHelper.StandardOptions);
                
                // Output to stdout or file
                if (outputPath == "stdout")
                {
                    Console.Write(formattedJson);
                }
                else
                {
                    await File.WriteAllTextAsync(outputPath, formattedJson, Encoding.UTF8);
                    Console.WriteLine($"✅ Extracted GameModel[{index}] to: {outputPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Error extracting GameModel: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Extracts the current GameModel from a .catan file and saves it as JSON.
        /// </summary>
        /// <param name="inputPath">Path to the input .catan file</param>
        /// <param name="outputPath">Path to the output JSON file</param>
        public static async Task ExtractGameModelAsync(string inputPath, string outputPath)
        {
            try
            {
                Console.WriteLine($"Extracting GameModel from: {inputPath}");
                
                // Validate input file exists
                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException($"Input file not found: {inputPath}");
                }
                
                // Read and decompress the .catan file
                var compressedBytes = await File.ReadAllBytesAsync(inputPath);
                var decompressedJson = Decompress(compressedBytes);
                
                // Parse the decompressed data (this is a SerializableLog)
                using var document = JsonDocument.Parse(decompressedJson);
                var root = document.RootElement;
                
                // The .catan file contains a SerializableLog with doneStack, redoStack, gameType, etc.
                if (!root.TryGetProperty("doneStack", out var doneStack))
                {
                    // Trace available properties for debugging
                    var properties = root.EnumerateObject().Select(p => p.Name).ToArray();
                    Console.WriteLine($"Available properties in .catan file: {string.Join(", ", properties)}");
                    throw new InvalidOperationException("No 'doneStack' property found in the .catan file");
                }
                
                var doneStackArray = doneStack.EnumerateArray().ToArray();
                if (doneStackArray.Length == 0)
                {
                    throw new InvalidOperationException("DoneStack is empty - no GameModel to extract");
                }
                
                // Get the first element (most recent due to LIFO storage) GameModel JSON string
                var gameModelJson = doneStackArray[0].GetString();
                if (string.IsNullOrEmpty(gameModelJson))
                {
                    throw new InvalidOperationException("GameModel JSON is null or empty");
                }
                
                // Parse and pretty-print the GameModel
                using var gameModelDoc = JsonDocument.Parse(gameModelJson);
                var prettyJson = JsonSerializer.Serialize(gameModelDoc.RootElement, JsonHelper.PrettyOptions);
                
                // Write to output file
                await File.WriteAllTextAsync(outputPath, prettyJson, Encoding.UTF8);
                
                // Report success
                var fileInfo = new FileInfo(outputPath);
                Console.WriteLine($"✅ Successfully extracted GameModel to: {outputPath}");
                Console.WriteLine($"   File size: {fileInfo.Length / 1024.0:F1} KB");
                
                // Show game metadata
                var gameModel = gameModelDoc.RootElement;
                if (gameModel.TryGetProperty("GameType", out var gameType))
                {
                    Console.WriteLine($"   Game Type: {gameType}");
                }
                if (gameModel.TryGetProperty("GameState", out var gameState))
                {
                    Console.WriteLine($"   Game State: {gameState}");
                }
                if (gameModel.TryGetProperty("Players", out var players))
                {
                    Console.WriteLine($"   Players: {players.GetArrayLength()}");
                }
                if (gameModel.TryGetProperty("CurrentPlayerIndex", out var currentPlayer))
                {
                    Console.WriteLine($"   Current Player Index: {currentPlayer}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error extracting GameModel: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Creates a combined .catan_test file from a .catan file and action JSON.
        /// </summary>
        /// <param name="catanPath">Path to the input .catan file</param>
        /// <param name="actionsPath">Path to the JSON file containing actions</param>
        /// <param name="outputPath">Path to the output .catan_test file</param>
        public static async Task CreateTestFileAsync(string catanPath, string actionsPath, string outputPath)
        {
            try
            {
                Console.WriteLine($"Creating .catan_test file:");
                Console.WriteLine($"  GameModel from: {catanPath}");
                Console.WriteLine($"  Actions from: {actionsPath}");
                
                // Extract GameModel from .catan file
                var compressedBytes = await File.ReadAllBytesAsync(catanPath);
                var decompressedJson = Decompress(compressedBytes);
                
                // Parse the decompressed data (this is a SerializableLog)
                using var catanDoc = JsonDocument.Parse(decompressedJson);
                var doneStack = catanDoc.RootElement.GetProperty("doneStack");
                
                if (doneStack.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("DoneStack is empty - no GameModel to extract");
                }
                
                // Get the first element (most recent due to LIFO storage) GameModel JSON string
                var gameModelJson = doneStack[0].GetString()!;
                using var gameModelDoc = JsonDocument.Parse(gameModelJson);
                
                // Read actions from JSON file
                var actionsJson = await File.ReadAllTextAsync(actionsPath);
                using var actionsDoc = JsonDocument.Parse(actionsJson);
                
                JsonElement actionStack;
                // Check if it's a scenario file with an "actions" property or direct action array
                if (actionsDoc.RootElement.TryGetProperty("actions", out var actions))
                {
                    actionStack = actions;
                }
                else if (actionsDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    actionStack = actionsDoc.RootElement;
                }
                else
                {
                    throw new InvalidOperationException("Actions file must contain either an 'actions' array or be an array itself");
                }
                
                // Create combined structure
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions 
                { 
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                writer.WriteStartObject();
                writer.WritePropertyName("GameModel");
                gameModelDoc.RootElement.WriteTo(writer);
                writer.WritePropertyName("ActionStack");
                actionStack.WriteTo(writer);
                writer.WriteEndObject();
                
                writer.Flush();
                
                // Save to file
                var combinedJson = Encoding.UTF8.GetString(stream.ToArray());
                await File.WriteAllTextAsync(outputPath, combinedJson, Encoding.UTF8);
                
                // Report success
                var fileInfo = new FileInfo(outputPath);
                Console.WriteLine($"✅ Successfully created .catan_test file: {outputPath}");
                Console.WriteLine($"   File size: {fileInfo.Length / 1024.0:F1} KB");
                Console.WriteLine($"   Actions in stack: {actionStack.GetArrayLength()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating test file: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Decompresses Brotli-compressed data to a JSON string.
        /// </summary>
        private static string Decompress(byte[] data)
        {
            using var compressedStream = new MemoryStream(data);
            using var brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            brotliStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
    }
}