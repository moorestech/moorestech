using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    /// <summary>
    ///     々
    /// </summary>
    public class PacketBufferParser
    {
        private List<byte> _continuationFromLastTimeBytes = new();

        private bool _isGettingLength;
        private int _nextPacketLengthOffset;
        private int _packetLength;
        private readonly List<byte> _packetLengthBytes = new();
        private int _remainingHeaderLength;

        public List<List<byte>> Parse(byte[] packet, int length)
        {
            
            var actualStartPacketDataIndex = 0;
            var reminderLength = length;

            var result = new List<List<byte>>();

            
            while (0 < reminderLength)
            {
                
                if (_continuationFromLastTimeBytes.Count == 0)
                {
                    
                    if (TryGetLength(packet, actualStartPacketDataIndex, out var payloadLength, out var headerLength))
                    {
                        _packetLength = payloadLength;
                        //short4
                        reminderLength -= _packetLength　+ headerLength;
                        actualStartPacketDataIndex += headerLength;
                    }
                    else
                    {
                        
                        break;
                    }
                }
                else
                {
                    
                    _packetLength -= _nextPacketLengthOffset;
                    reminderLength = length - _packetLength;
                }

                
                if (reminderLength < 0)
                {
                    var addCollection = packet.Skip(actualStartPacketDataIndex).ToList();
                    _continuationFromLastTimeBytes.AddRange(addCollection);
                    
                    _nextPacketLengthOffset = length - actualStartPacketDataIndex;
                    break;
                }

                
                for (var i = 0; i < _packetLength && actualStartPacketDataIndex < length; actualStartPacketDataIndex++, i++) _continuationFromLastTimeBytes.Add(packet[actualStartPacketDataIndex]);

                result.Add(_continuationFromLastTimeBytes);
                
                _continuationFromLastTimeBytes = new List<byte>();
            }

            return result;
        }


        private bool TryGetLength(byte[] bytes, int startIndex, out int payloadLength, out int headerLength)
        {
            var headerBytes = new List<byte>();
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
                    bytes[startIndex + 3]
                };
            }


            if (BitConverter.IsLittleEndian) headerBytes.Reverse();

            payloadLength = BitConverter.ToInt32(headerBytes.ToArray(), 0);
            return true;
        }
    }
}