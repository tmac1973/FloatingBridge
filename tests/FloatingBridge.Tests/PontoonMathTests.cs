using FloatingBridge.Core;
using Xunit;

namespace FloatingBridge.Tests
{
    public class PontoonMathTests
    {
        [Fact]
        public void DeckRootY_puts_deck_surface_at_height_above_water()
        {
            // Deck surface 0.1 above the root, wanted 0.5 above sea level 30: root at 30.4.
            float rootY = PontoonMath.DeckRootY(30f, 0.5f, 0.1f);
            Assert.Equal(30.4f, rootY, 4);
            Assert.Equal(30.5f, rootY + 0.1f, 4);
        }

        [Fact]
        public void DeckRootY_with_zero_height_puts_deck_at_sea_level()
        {
            Assert.Equal(30f, PontoonMath.DeckRootY(30f, 0f, 0f), 4);
        }

        [Theory]
        [InlineData(20f, true)]     // deep water
        [InlineData(29.5f, true)]   // exactly min depth
        [InlineData(29.8f, false)]  // too shallow
        [InlineData(35f, false)]    // dry land
        public void HasEnoughWater(float ground, bool expected)
        {
            Assert.Equal(expected, PontoonMath.HasEnoughWater(ground, 30f, 0.5f));
        }
    }
}
