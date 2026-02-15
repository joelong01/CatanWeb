using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.Shared
{
    public class HexCoordinatesLayoutTests
    {
        // ================================================================
        // Spiral coordinate tests
        // ================================================================

        [Fact]
        public void SpiralCoordinates_Count0_ReturnsEmpty()
        {
            var result = HexCoordinates.GenerateSpiralCoordinates(0);
            Assert.Empty(result);
        }

        [Fact]
        public void SpiralCoordinates_Count1_ReturnsCenter()
        {
            var result = HexCoordinates.GenerateSpiralCoordinates(1);
            Assert.Single(result);
            Assert.Equal(new HexCoordinates(0, 0, 0), result[0]);
        }

        [Fact]
        public void SpiralCoordinates_Count7_ReturnsCenterPlusRing1()
        {
            var result = HexCoordinates.GenerateSpiralCoordinates(7);
            Assert.Equal(7, result.Count);
            Assert.Equal(new HexCoordinates(0, 0, 0), result[0]);
            // First ring starts at North (0, -1, 1)
            Assert.Equal(new HexCoordinates(0, -1, 1), result[1]);
        }

        [Fact]
        public void SpiralCoordinates19_MatchesRegularBoardSet()
        {
            var spiral = HexCoordinates.GenerateSpiralCoordinates(19);
            var regular = RegularBoardInfo.Default.TileKeys;

            Assert.Equal(19, spiral.Count);

            var sortedSpiral = spiral.OrderBy(c => c.Q).ThenBy(c => c.R).ToList();
            var sortedRegular = regular.OrderBy(c => c.Q).ThenBy(c => c.R).ToList();

            Assert.Equal(sortedRegular.Count, sortedSpiral.Count);
            for (int i = 0; i < sortedRegular.Count; i++)
            {
                Assert.Equal(sortedRegular[i].Q, sortedSpiral[i].Q);
                Assert.Equal(sortedRegular[i].R, sortedSpiral[i].R);
                Assert.Equal(sortedRegular[i].S, sortedSpiral[i].S);
            }
        }

        [Fact]
        public void SpiralCoordinates_AllSatisfyCubeConstraint()
        {
            var coords = HexCoordinates.GenerateSpiralCoordinates(37);
            foreach (var c in coords)
            {
                Assert.Equal(0, c.Q + c.R + c.S);
            }
        }

        [Fact]
        public void SpiralCoordinates_NoDuplicates()
        {
            var coords = HexCoordinates.GenerateSpiralCoordinates(37);
            var set = new HashSet<string>(coords.Select(c => $"{c.Q},{c.R},{c.S}"));
            Assert.Equal(coords.Count, set.Count);
        }

        // ================================================================
        // Square coordinate tests - column heights
        // ================================================================

        [Theory]
        [InlineData(19, new[] { 3, 4, 5, 4, 3 })]
        [InlineData(30, new[] { 3, 4, 5, 6, 5, 4, 3 })]
        [InlineData(11, new[] { 4, 3, 4 })]
        [InlineData(1, new[] { 1 })]
        [InlineData(7, new[] { 2, 3, 2 })]  // hill c=3 k=1: b=(7-1)/3=2 → [2,3,2]=7
        public void SquareCoordinates_ProducesExpectedColumnHeights(int count, int[] expectedHeights)
        {
            var heights = HexCoordinates.ComputeSquareColumnHeights(count);
            Assert.Equal(expectedHeights, heights);
        }

        [Fact]
        public void SquareCoordinates_AdjacentColumnsDifferByAtMost1()
        {
            // Test a range of counts
            foreach (int count in new[] { 3, 7, 10, 11, 12, 19, 25, 30, 37, 43 })
            {
                var heights = HexCoordinates.ComputeSquareColumnHeights(count);
                Assert.Equal(count, heights.Sum());
                for (int i = 1; i < heights.Length; i++)
                {
                    Assert.True(
                        Math.Abs(heights[i] - heights[i - 1]) <= 1,
                        $"Count={count}: heights[{i - 1}]={heights[i - 1]}, heights[{i}]={heights[i]}, diff={Math.Abs(heights[i] - heights[i - 1])}"
                    );
                }
            }
        }

        // ================================================================
        // Square coordinate tests - coordinate generation
        // ================================================================

        [Fact]
        public void SquareCoordinates30_MatchesExpansionBoardSet()
        {
            var square = HexCoordinates.GenerateSquareCoordinates(30);
            var expansion = ExpansionBoardInfo.Default.TileKeys;

            Assert.Equal(30, square.Count);

            var sortedSquare = square.OrderBy(c => c.Q).ThenBy(c => c.R).ToList();
            var sortedExpansion = expansion.OrderBy(c => c.Q).ThenBy(c => c.R).ToList();

            Assert.Equal(sortedExpansion.Count, sortedSquare.Count);
            for (int i = 0; i < sortedExpansion.Count; i++)
            {
                Assert.Equal(sortedExpansion[i].Q, sortedSquare[i].Q);
                Assert.Equal(sortedExpansion[i].R, sortedSquare[i].R);
                Assert.Equal(sortedExpansion[i].S, sortedSquare[i].S);
            }
        }

        [Fact]
        public void SquareCoordinates_AllSatisfyCubeConstraint()
        {
            foreach (int count in new[] { 1, 7, 11, 19, 30 })
            {
                var coords = HexCoordinates.GenerateSquareCoordinates(count);
                Assert.Equal(count, coords.Count);
                foreach (var c in coords)
                {
                    Assert.Equal(0, c.Q + c.R + c.S);
                }
            }
        }

        [Fact]
        public void SquareCoordinates_NoDuplicates()
        {
            foreach (int count in new[] { 11, 19, 30 })
            {
                var coords = HexCoordinates.GenerateSquareCoordinates(count);
                var set = new HashSet<string>(coords.Select(c => $"{c.Q},{c.R},{c.S}"));
                Assert.Equal(coords.Count, set.Count);
            }
        }

        [Fact]
        public void SquareCoordinates_Count0_ReturnsEmpty()
        {
            Assert.Empty(HexCoordinates.GenerateSquareCoordinates(0));
        }

        [Fact]
        public void SquareCoordinates_Count1_ReturnsCenter()
        {
            var result = HexCoordinates.GenerateSquareCoordinates(1);
            Assert.Single(result);
            Assert.Equal(new HexCoordinates(0, 0, 0), result[0]);
        }
    }
}
