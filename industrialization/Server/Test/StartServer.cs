using industrialization.Server.PacketHandle;
using NUnit.Framework;

namespace industrialization.Server.Test
{
    public class StartServer
    {
        [Test]
        public void StartTest()
        {
            PacketHandler.StartServer();
            Assert.True(true);
        }
    }
}