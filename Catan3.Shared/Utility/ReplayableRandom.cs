using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catan3.Shared.Utility
{
    public sealed class ReplayableRandom
    {
        private readonly int _seed;
        private Random _rng = null!;

        public int Iterations { get; private set; }

        public ReplayableRandom(int seed, int inter = 0)
        {
            _seed = seed;
            Iterations = 0;
            Restore(inter);
        }

        // Call this on load: resets and advances to recorded Iterations
        public void Restore(int iterations)
        {
            _rng = new Random(_seed);
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
