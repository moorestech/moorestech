using NUnit.Framework;

namespace Test.UnitTest.Game
{
    public class MultiSizeBlockTest
    {
        public const int Block_1x4_Id = 1;
        public const int Block_3x2_Id = 2;

        /// <summary>
        /// Place blocks in each direction, east-west, north-south and south-south, and test that the blocks can and cannot be retrieved at the boundaries.
        /// </summary>
        [Test]
        public void BlockPlaceAndGetTest()
        {
        }
    }
}