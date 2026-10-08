using NUnit.Framework;

namespace DragonMMA.Tests
{
    public sealed class OverlaySizingTests
    {
        [TestCase(1080, 1040, false, 520)]
        [TestCase(1080, 1040, true, 720)]
        [TestCase(1440, 1392, false, 693)]
        [TestCase(1440, 1392, true, 960)]
        [TestCase(2160, 2080, true, 1440)]
        [TestCase(1080, 500, true, 500)]
        public void DockHeight_ScalesAndNeverCoversTaskbar(int monitor, int work, bool expanded, int expected)
        {
            Assert.That(DesktopOverlayWindow.CalculateDockHeight(monitor, work, expanded), Is.EqualTo(expected));
        }
    }
}
