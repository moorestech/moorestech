using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    /// <summary>
    /// 複数のパケットがバッファーに入っていた場合にそれらのパケットを別々のパケットに分割するクラス
    /// </summary>
    public class PacketBufferParser
    {
        List<byte> _continuationFromLastTimeBytes = new();
        int _packetLength = 0;
        int _nextPacketLengthOffset = 0;
        
        public List<List<byte>> Parse(byte[] packet,int length)
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
                    _packetLength = GetLength(packet, actualStartPacketDataIndex);
                    //パケット長のshort型の4バイトを取り除く
                    reminderLength -= _packetLength　+ 4;
                    actualStartPacketDataIndex += 4;
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
                for (var i = 0; i < _packetLength && actualStartPacketDataIndex < length; actualStartPacketDataIndex++,i++)
                {
                    _continuationFromLastTimeBytes.Add(packet[actualStartPacketDataIndex]);
                }
                        
                result.Add(_continuationFromLastTimeBytes);
                //受信したパケットに対する応答を返す
                _continuationFromLastTimeBytes = new();
            }

            return result;
        }
        
        
        private int GetLength(byte[] bytes,int startIndex)
        {
            var b = new List<byte>
            {
                bytes[startIndex],
                bytes[startIndex + 1],
                bytes[startIndex + 2],
                bytes[startIndex + 3]
            };

            if (BitConverter.IsLittleEndian) b.Reverse();
            
            return BitConverter.ToInt32(b.ToArray(), 0);
        }
    }
}