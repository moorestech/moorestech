using System;
using System.Net.Sockets;
using System.Threading;
using Server.Protocol;
using Server.Util;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class UserPacketHandler
    {
        private readonly Socket _client;
        private readonly PacketQueueProcessor _packetQueueProcessor;
        
        public UserPacketHandler(Socket client, PacketResponseCreator packetResponseCreator)
        {
            _client = client;
            _packetQueueProcessor = new PacketQueueProcessor(_client, packetResponseCreator);
        }
        
        
        public void StartListen(CancellationToken token)
        {
            var buffer = new byte[4096];
            //切断されるまでパケットを受信
            try
            {
                var parser = new PacketBufferParser();
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var error = ReceiveProcess(parser, buffer);
                    if (error)
                    {
                        Debug.Log("切断されました");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _packetQueueProcessor.Dispose();
                _client.Close();
                Debug.Log("切断されました");
            }
            catch (Exception e)
            {
                _packetQueueProcessor.Dispose();
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
            
            // パケット処理はメインスレッドに委譲
            foreach (var packet in packets)
            {
                _packetQueueProcessor.EnqueuePacket(packet);
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