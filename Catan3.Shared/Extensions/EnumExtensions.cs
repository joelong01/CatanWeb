using System.ComponentModel;
using System.Reflection;

namespace Catan3.Shared.Extensions
{
    /// <summary>
    /// Extension methods for enums, providing access to Description attributes.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the Description attribute value for an enum value.
        /// Falls back to ToString() if no Description attribute is present.
        /// </summary>
        /// <param name="instance">The enum value.</param>
        /// <returns>The description string or the enum's ToString() value.</returns>
        public static string Description(this Enum instance)
        {
            Type type = instance.GetType();
            if (type is null) return string.Empty;

            FieldInfo? fi = type.GetField(instance.ToString());
            if (fi is null) return string.Empty;

            DescriptionAttribute[]? attrs = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attrs is not null && attrs.Length > 0)
            {
                return attrs[0].Description;
            }

            return instance.ToString();
        }
    }
}
