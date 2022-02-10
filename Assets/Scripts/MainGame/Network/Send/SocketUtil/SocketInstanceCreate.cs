using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MainGame.Network.Send.SocketUtil
{
    public class SocketInstanceCreate
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _remoteEndPoint;
        public SocketInstanceCreate(ConnectionServerConfig config)
        {
            //IPアドレスやポートを設定
            IPAddress ipAddress = null;
            if (IPAddress.TryParse(config.IP,out ipAddress))
            {
            }
            else if(IsUrl(config.IP))
            {
                ipAddress = Dns.GetHostEntry(config.IP).AddressList[0].MapToIPv4();
            }
            else
            {
                Debug.LogError("IP解析失敗");
                return;
            }
            //ソケットを作成
            _remoteEndPoint = new IPEndPoint(ipAddress, config.Port);
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        
        public Socket GetSocket()
        {
            return _socket;
        }
        
        public IPEndPoint GetRemoteEndPoint()
        {
            return _remoteEndPoint;
        }
        
        
        private static bool IsUrl( string input )
        {
            if ( string.IsNullOrEmpty( input ) )
            {
                return false;
            }
            return Regex.IsMatch( 
                input, 
                @"[-_.!~*'()a-zA-Z0-9;/?:@&=+$,%#]+$" 
            );
        }
        
    }
}