using System;
using System.Net.Sockets;
using Server.Protocol;
using Server.Util;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class UserResponse
    {
        private readonly Socket _client;
        private readonly PacketResponseCreator _packetResponseCreator;
        private int _byteCount;
        
        private DateTime _startTime;
        
        public UserResponse(Socket client, PacketResponseCreator packetResponseCreator)
        {
            _packetResponseCreator = packetResponseCreator;
            _client = client;
        }
        
        
        public void StartListen()
        {
            _startTime = DateTime.Now;
            
            var buffer = new byte[4096];
            //切断されるまでパケットを受信
            try
            {
                var parser = new PacketBufferParser();
                while (true)
                {
                    var error = ReceiveProcess(parser, buffer);
                    if (error)
                    {
                        Debug.Log("切断されました");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _client.Close();
                Debug.LogError("moorestech内のロジックによるエラーで切断");
                Debug.LogException(e);
            }
        }
        
        
        private bool ReceiveProcess(PacketBufferParser parser, byte[] buffer)
        {
            var length = _client.Receive(buffer);
            if (length == 0) return true;
            
            //受信データをパケットに分割
            var packets = parser.Parse(buffer, length);
            
            foreach (var packet in packets)
            {
                var results = _packetResponseCreator.GetPacketResponse(packet);
                foreach (var result in results)
                {
                    result.InsertRange(0, ToByteList.Convert(result.Count));
                    var array = result.ToArray();
                    _byteCount += array.Length;
                    _client.Send(array);
                }
            }
            
            //LogDataConsumption(_byteCount, _startTime);
            
            return false;
        }
        
        public static void LogDataConsumption(int bytesSent, DateTime startTime)
        {
            // Convert bytes to Megabytes
            var megabytesSent = (double)bytesSent / 1024;
            
            // Calculate elapsed time in seconds
            var elapsedTimeSeconds = (DateTime.Now - startTime).TotalSeconds;
            
            // Calculate avg bandwidth in MB/s
            var avgBandwidth = megabytesSent / elapsedTimeSeconds;
            
            // Output the result
            Debug.Log($"送信量 {megabytesSent:F1} KB 平均消費帯域 {avgBandwidth:F1} KB/s 時間 {elapsedTimeSeconds}");
        }
    }
}