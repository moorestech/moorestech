using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public class PacketParser
    {
        List<byte> _protocol = new();
        int _packetLength = 0;
        int _nextPacketLengthOffset = 0;
        
        public List<List<byte>> Parse(byte[] packet,int length)
        {
            //プロトコル長から実際のプロトコルを作る
            var packetIndex = 0;
            var reminderLength = length;
            
            var result = new List<List<byte>>();
            
            //受信したパケットの最後までループ
            while (0 < reminderLength)
            {
                //前回からの続きのデータがない場合
                if (_protocol.Count == 0)
                {
                    //パケット長を取得
                    _packetLength = GetLength(packet, packetIndex);
                    //パケット長のshort型の2バイトを取り除く
                    reminderLength -= _packetLength　+ 2;
                    packetIndex += 2;
                }
                else
                {
                    //前回からの続きのデータがある場合
                    _packetLength = _packetLength - _nextPacketLengthOffset;
                    reminderLength = length - _packetLength;
                }

                //パケットが切れているので、残りのデータを一時保存
                if (reminderLength < 0)
                {
                    _protocol.AddRange(packet.Skip(packetIndex));
                    //次回の受信のためにどこからデータを保存するかのオフセットを保存
                    _nextPacketLengthOffset = length - packetIndex;
                    break;
                }
                        
                for (int i = 0; i < _packetLength && packetIndex < length; packetIndex++,i++)
                {
                    _protocol.Add(packet[packetIndex]);
                }
                        
                result.Add(_protocol);
                //受信したパケットに対する応答を返す
                _protocol = new();
            }

            return result;
        }
        
        
        private short GetLength(byte[] bytes,int startIndex)
        {
            var b = new List<byte>();
            b.Add(bytes[startIndex]);
            b.Add(bytes[startIndex + 1]);

            if (BitConverter.IsLittleEndian) b.Reverse();
            
            return BitConverter.ToInt16(b.ToArray(), 0);
        }
    }
}