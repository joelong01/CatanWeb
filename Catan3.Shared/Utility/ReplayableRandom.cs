using System.Text.Json.Serialization;

namespace Catan3.Shared.Utility
{
    public sealed class ReplayableRandom
    {
        [JsonIgnore]
        private Random _rng = null!;

        public int Iterations { get; private set; }
        public int Seed { get; private set; }

        [JsonConstructor]
        public ReplayableRandom(int seed, int iterations = 0)
        {
            Seed = seed;
            Restore(iterations);
        }

        // Default constructor for truly random games (not deterministic)
        public ReplayableRandom() : this(Random.Shared.Next())
        {
        }

        // Call this on load: resets and advances to recorded Iterations
        public void Restore(int iterations)
        {
            _rng = new Random(Seed);
            Iterations = 0;
            for (int i = 0; i < iterations; i++)
            {
                _rng.Next(); // burn values to advance state
                Iterations++;
            }
        }

        public int Next()
        {
            Iterations++;
            return _rng.Next();
        }

        public int Next(int maxValue)
        {
            Iterations++;
            return _rng.Next(maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            Iterations++;
            return _rng.Next(minValue, maxValue);
        }

        public double NextDouble()
        {
            Iterations++;
            return _rng.NextDouble();
        }
    }

}
