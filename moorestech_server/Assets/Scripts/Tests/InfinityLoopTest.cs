using NUnit.Framework;

namespace Tests
{
    public class InfinityLoopTest
    {
        [Test]
        [Timeout(3000)]
        public void InfinityLoop()
        {
            while (true)
            {
                
            }
        }
    }
}