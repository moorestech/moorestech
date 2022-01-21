using System.Net.Sockets;
using MainGame.Network.Interface;

namespace Test.TestModule
{
    public class TestSocketModule : ISocket
    {
        public byte[] SentData;

        public void Send(byte[] array)
        {
            SentData = array;
        }
    }
}