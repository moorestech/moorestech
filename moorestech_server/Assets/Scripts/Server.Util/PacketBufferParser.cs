using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    /// <summary>
    ///     複数のパケットがバッファーに入っていた場合にそれらのパケットを別々のパケットに分割するクラス
    /// </summary>
    public class PacketBufferParser
    {
        private readonly List<byte> _packetLengthBytes = new();
        private List<byte> _continuationFromLastTimeBytes = new();
        
        private bool _isGettingLength;
        private int _nextPacketLengthOffset;
        private int _packetLength;
        private int _remainingHeaderLength;
        
        public List<List<byte>> Parse(byte[] packet, int length)
        {
            //プロトコル長から実際のプロトコルを作る
            var actualStartPacketDataIndex = 0;
            var reminderLength = length;
            
            var result = new List<List<byte>>();
            
            //受信したパケットの最後までループ
            while (0 < reminderLength)
            {
                //前回からの続きのデータがない場合
                if (_continuationFromLastTimeBytes.Count == 0)
                {
                    //パケット長を取得
                    if (TryGetLength(packet, actualStartPacketDataIndex, out var payloadLength, out var headerLength))
                    {
                        _packetLength = payloadLength;
                        //パケット長のshort型の4バイトを取り除く
                        reminderLength -= _packetLength + headerLength;
                        actualStartPacketDataIndex += headerLength;
                    }
                    else
                    {
                        //残りバッファサイズ的に取得できない場合は次回の受信で取得する
                        break;
                    }
                }
                else
                {
                    //前回からの続きのデータがある場合
                    _packetLength -= _nextPacketLengthOffset;
                    reminderLength = length - _packetLength;
                }
                
                //パケットが切れているので、残りのデータを一時保存
                if (reminderLength < 0)
                {
                    var addCollection = packet.Skip(actualStartPacketDataIndex).ToList();
                    _continuationFromLastTimeBytes.AddRange(addCollection);
                    //次回の受信のためにどこからデータを保存するかのオフセットを保存
                    _nextPacketLengthOffset = length - actualStartPacketDataIndex;
                    break;
                }
                
                //パケットの長さ分だけデータを取得
                for (var i = 0;
                     i < _packetLength && actualStartPacketDataIndex < length;
                     actualStartPacketDataIndex++, i++)
                    _continuationFromLastTimeBytes.Add(packet[actualStartPacketDataIndex]);
                
                result.Add(_continuationFromLastTimeBytes);
                //受信したパケットに対する応答を返す
                _continuationFromLastTimeBytes = new List<byte>();
            }
            
            return result;
        }
        
        
        private bool TryGetLength(byte[] bytes, int startIndex, out int payloadLength, out int headerLength)
        {
            List<byte> headerBytes;
            if (_isGettingLength)
            {
                headerLength = _remainingHeaderLength;
                for (var i = 0; i < _remainingHeaderLength; i++) _packetLengthBytes.Add(bytes[i]);
                headerBytes = _packetLengthBytes;
                _isGettingLength = false;
            }
            else
            {
                payloadLength = -1;
                headerLength = -1;
                //パケット長が取得でききれない場合
                if (bytes.Length <= startIndex + 3)
                {
                    _packetLengthBytes.Clear();
                    _remainingHeaderLength = 4;
                    for (var i = startIndex; i < bytes.Length; i++)
                    {
                        _remainingHeaderLength = 3 - (i - startIndex);
                        _packetLengthBytes.Add(bytes[i]);
                    }
                    
                    _isGettingLength = true;
                    return false;
                }
                
                headerLength = 4;
                headerBytes = new List<byte>
                {
                    bytes[startIndex],
                    bytes[startIndex + 1],
                    bytes[startIndex + 2],
                    bytes[startIndex + 3],
                };
            }
            
            
            if (BitConverter.IsLittleEndian) headerBytes.Reverse();
            
            payloadLength = BitConverter.ToInt32(headerBytes.ToArray(), 0);
            return true;
        }
    }
}