using System.Net.Sockets;

namespace Test.TestModule
{
    public class TestSocketModule : Socket
    {
        public byte[] SentData;
        public TestSocketModule(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
        {
        }

        public TestSocketModule(SocketInformation socketInformation) : base(socketInformation)
        {
        }

        public TestSocketModule(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
        {
        }

        public new int Send(byte[] array, int offset, int size, SocketFlags socketFlags)
        {
            SentData = array;
            return 0;
        }
    }
}