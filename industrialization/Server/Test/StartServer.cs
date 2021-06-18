using NUnit.Framework;

namespace industrialization.Server.Test
{
    public class StartServer
    {
        [Test]
        public void StartTest()
        {
            AsynchronousSocketListener.StartListening();
            Assert.True(true);
        }
    }
}