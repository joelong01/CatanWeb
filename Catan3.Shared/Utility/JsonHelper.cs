using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Centralized JSON serialization helper to ensure consistent serialization across all projects.
    /// This eliminates the need for each project to define its own JsonSerializerOptions and 
    /// prevents serialization inconsistencies that cause test failures.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Standard JsonSerializerOptions used across all projects in the solution.
        /// Configured for JavaScript compatibility and consistent enum handling.
        /// </summary>
        public static readonly JsonSerializerOptions StandardOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false, // Compact by default for performance
            Converters = { new JsonStringEnumConverter() },
            // Additional options for robustness
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            // Increase validation limits for complex GameModel objects
            MaxDepth = 128, // Default is 64, increase for nested game structures
            DefaultBufferSize = 32 * 1024, // 32KB buffer size
            ReferenceHandler = ReferenceHandler.IgnoreCycles // Handle circular references
        };

        /// <summary>
        /// Pretty-printed JsonSerializerOptions for debugging and test output.
        /// Same configuration as StandardOptions but with indented formatting.
        /// </summary>
        public static readonly JsonSerializerOptions PrettyOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true, // Pretty formatting for readability
            Converters = { new JsonStringEnumConverter() },
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            // Increase validation limits for complex GameModel objects
            MaxDepth = 128, // Default is 64, increase for nested game structures
            DefaultBufferSize = 32 * 1024, // 32KB buffer size
            ReferenceHandler = ReferenceHandler.IgnoreCycles // Handle circular references
        };

        /// <summary>
        /// Serializes an object using the standard JSON options.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="value">The object to serialize</param>
        /// <param name="pretty">Whether to use pretty formatting (default: false)</param>
        /// <returns>JSON string representation of the object</returns>
        public static string Serialize<T>(T value, bool pretty = false)
        {
            return JsonSerializer.Serialize(value, pretty ? PrettyOptions : StandardOptions);
        }

        /// <summary>
        /// Deserializes a JSON string using the standard JSON options.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>Deserialized object of type T</returns>
        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, StandardOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to a specific type using the standard JSON options.
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <param name="returnType">The type to deserialize to</param>
        /// <returns>Deserialized object of the specified type</returns>
        public static object? Deserialize(string json, Type returnType)
        {
            return JsonSerializer.Deserialize(json, returnType, StandardOptions);
        }

        /// <summary>
        /// Copies the standard JsonSerializerOptions settings to another JsonSerializerOptions instance.
        /// Use this method to configure ASP.NET Core or SignalR JsonSerializerOptions.
        /// </summary>
        /// <param name="target">The JsonSerializerOptions to configure</param>
        public static void ConfigureOptions(JsonSerializerOptions target)
        {
            target.PropertyNameCaseInsensitive = StandardOptions.PropertyNameCaseInsensitive;
            target.PropertyNamingPolicy = StandardOptions.PropertyNamingPolicy;
            target.WriteIndented = StandardOptions.WriteIndented;
            target.AllowTrailingCommas = StandardOptions.AllowTrailingCommas;
            target.ReadCommentHandling = StandardOptions.ReadCommentHandling;
            target.MaxDepth = StandardOptions.MaxDepth;
            target.DefaultBufferSize = StandardOptions.DefaultBufferSize;
            target.ReferenceHandler = StandardOptions.ReferenceHandler;
            
            // Copy converters
            target.Converters.Clear();
            foreach (var converter in StandardOptions.Converters)
            {
                target.Converters.Add(converter);
            }
        }
    }
}