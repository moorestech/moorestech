using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Protocol;

namespace Server.Boot.PacketHandle
{
    public class PacketHandler
    {
        private const int Port = 11564;

        public void StartServer(PacketResponseCreator packetResponseCreator)
        {
            
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(10);
            Console.WriteLine("moorestech ");

            while (true)
            {
                
                var client = listener.Accept();
                Console.WriteLine("");
                
                Task.Run(() => new UserResponse(client, packetResponseCreator).StartListen());
            }
        }
    }
}