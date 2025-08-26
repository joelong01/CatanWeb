using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Provides serialization and compression utilities for game data.
    /// Combines JSON serialization with Brotli compression for efficient storage.
    /// </summary>
    public static class SerializationHelper
    {
        /// <summary>
        /// JSON serializer options for consistent serialization across the application.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Serializes an object to JSON string using consistent options.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <returns>JSON string representation of the object</returns>
        public static string JsonSerialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, JsonOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to the specified type using consistent options.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>Deserialized object of type T, or null if deserialization fails</returns>
        public static T? JsonDeserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        /// <summary>
        /// Compresses a text string using Brotli compression.
        /// </summary>
        /// <param name="text">The text to compress</param>
        /// <returns>Compressed data as byte array</returns>
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

        /// <summary>
        /// Decompresses Brotli-compressed data to a text string.
        /// </summary>
        /// <param name="data">The compressed data to decompress</param>
        /// <returns>Decompressed text string</returns>
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