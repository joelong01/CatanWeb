using System;
using System.Text;
namespace Catan3.Utility
{

    public class UniqueIdGenerator
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static readonly Random Random = new Random();

        public static string GenerateUniqueId(int randomLength = 8)
        {
            // Get the current Unix timestamp in milliseconds
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Generate a random component
            var randomBytes = new byte[randomLength];
            Random.NextBytes(randomBytes);

            // Convert timestamp to Base62
            string timestampBase62 = Base62Encode((ulong)timestamp);

            // Convert random component to Base62
            string randomBase62 = Base62Encode(randomBytes);

            // Combine timestamp and random component
            return $"{timestampBase62}{randomBase62}";
        }

        private static string Base62Encode(ulong value)
        {
            var sb = new StringBuilder();
            do
            {
                uint index = (uint)value % 62;
                System.Diagnostics.Debug.Assert(index >= 0);
                System.Diagnostics.Debug.Assert(index < Base62Chars.Length);
                sb.Insert(0, Base62Chars[(int)(value % 62)]);
                value /= 62;
            } while (value > 0);
            return sb.ToString();
        }

        private static string Base62Encode(byte[] bytes)
        {
            // Pad the byte array to ensure it is at least 8 bytes long
            var paddedBytes = PadToLong(bytes);
            var value = BitConverter.ToUInt64(paddedBytes, 0);
            return Base62Encode((ulong)value);
        }
        private static byte[] PadToLong(byte[] bytes)
        {
            if (bytes.Length >= 8)
                return bytes;

            var paddedBytes = new byte[8];
            Buffer.BlockCopy(bytes, 0, paddedBytes, 8 - bytes.Length, bytes.Length);
            return paddedBytes;
        }
    }
}
