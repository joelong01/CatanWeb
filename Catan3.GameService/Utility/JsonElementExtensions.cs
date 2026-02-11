using System.Text.Json;

namespace Catan3.GameService.Utility
{
    /// <summary>
    /// Extension methods for JsonElement with descriptive error messages.
    /// Use these instead of direct GetProperty calls to get clear error messages
    /// that include the property name and parent context when parsing fails.
    /// </summary>
    public static class JsonElementExtensions
    {
        /// <summary>
        /// Gets a required property from a JsonElement, throwing a descriptive exception if not found.
        /// </summary>
        /// <param name="element">The parent JsonElement</param>
        /// <param name="propertyName">The property name to find</param>
        /// <param name="context">Optional context string for error messages (e.g., "roadKey", "messageData")</param>
        /// <returns>The JsonElement for the property</returns>
        /// <exception cref="JsonException">Thrown when property is not found</exception>
        public static JsonElement RequireProperty(this JsonElement element, string propertyName, string? context = null)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value;
            }

            var contextStr = string.IsNullOrEmpty(context) ? "" : $" in '{context}'";
            var availableProps = GetAvailableProperties(element);
            throw new JsonException(
                $"Required property '{propertyName}' not found{contextStr}. " +
                $"Available properties: [{availableProps}]. " +
                $"JSON: {element.GetRawText().Truncate(200)}");
        }

        /// <summary>
        /// Gets a required string property from a JsonElement.
        /// </summary>
        public static string RequireString(this JsonElement element, string propertyName, string? context = null)
        {
            var prop = element.RequireProperty(propertyName, context);
            var value = prop.GetString();
            if (value == null)
            {
                throw new JsonException($"Property '{propertyName}' is null but string was expected{ContextStr(context)}");
            }
            return value;
        }

        /// <summary>
        /// Gets a required integer property from a JsonElement.
        /// </summary>
        public static int RequireInt32(this JsonElement element, string propertyName, string? context = null)
        {
            var prop = element.RequireProperty(propertyName, context);
            if (prop.ValueKind != JsonValueKind.Number)
            {
                throw new JsonException(
                    $"Property '{propertyName}' expected to be a number but was {prop.ValueKind}{ContextStr(context)}. " +
                    $"Value: {prop.GetRawText()}");
            }
            return prop.GetInt32();
        }

        /// <summary>
        /// Gets a required boolean property from a JsonElement.
        /// </summary>
        public static bool RequireBool(this JsonElement element, string propertyName, string? context = null)
        {
            var prop = element.RequireProperty(propertyName, context);
            if (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
            {
                throw new JsonException(
                    $"Property '{propertyName}' expected to be a boolean but was {prop.ValueKind}{ContextStr(context)}. " +
                    $"Value: {prop.GetRawText()}");
            }
            return prop.GetBoolean();
        }

        /// <summary>
        /// Gets an optional string property from a JsonElement.
        /// </summary>
        public static string? OptionalString(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString();
            }
            return null;
        }

        /// <summary>
        /// Parses a required enum from a string property.
        /// </summary>
        public static TEnum RequireEnum<TEnum>(this JsonElement element, string propertyName, string? context = null)
            where TEnum : struct, Enum
        {
            var str = element.RequireString(propertyName, context);
            if (!Enum.TryParse<TEnum>(str, out var result))
            {
                var validValues = string.Join(", ", Enum.GetNames<TEnum>());
                throw new JsonException(
                    $"Invalid value '{str}' for enum {typeof(TEnum).Name}{ContextStr(context)}. " +
                    $"Valid values: [{validValues}]");
            }
            return result;
        }

        /// <summary>
        /// Parses HexCoordinates from a JsonElement containing q, r, s properties.
        /// </summary>
        public static Catan3.Shared.Utility.HexCoordinates RequireHexCoordinates(this JsonElement element, string propertyName, string? context = null)
        {
            var coords = element.RequireProperty(propertyName, context);
            var coordContext = $"{context}.{propertyName}";
            var q = coords.RequireInt32("q", coordContext);
            var r = coords.RequireInt32("r", coordContext);
            var s = coords.RequireInt32("s", coordContext);
            return new Catan3.Shared.Utility.HexCoordinates(q, r, s);
        }

        private static string GetAvailableProperties(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return $"(not an object, is {element.ValueKind})";
            }

            var props = new List<string>();
            foreach (var prop in element.EnumerateObject())
            {
                props.Add(prop.Name);
            }
            return props.Count > 0 ? string.Join(", ", props) : "(empty object)";
        }

        private static string ContextStr(string? context)
        {
            return string.IsNullOrEmpty(context) ? "" : $" in '{context}'";
        }

        private static string Truncate(this string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
                return str;
            return str.Substring(0, maxLength) + "...";
        }
    }
}
