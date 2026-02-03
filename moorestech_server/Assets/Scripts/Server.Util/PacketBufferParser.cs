using System;
using System.Collections.Generic;

namespace Server.Util
{
    /// <summary>
    /// 複数のパケットがバッファーに入っていた場合にそれらのパケットを別々のパケットに分割するクラス
    /// Parses multiple packets from a byte buffer, handling partial packets across multiple calls
    /// </summary>
    public class PacketBufferParser
    {
        private readonly List<byte> _packetLengthBytes = new();
        private List<byte> _continuationFromLastTimeBytes = new();

        private bool _isGettingLength;
        private bool _isReadingPayload;
        private int _nextPacketLengthOffset;
        private int _packetLength;
        private int _remainingHeaderLength;

        public List<List<byte>> Parse(byte[] packet, int length)
        {
            // プロトコル長から実際のプロトコルを作る
            // Parse actual protocol data from buffer
            var actualStartPacketDataIndex = 0;
            var reminderLength = length;

            var result = new List<List<byte>>();

            // 受信したパケットの最後までループ
            // Loop until all received data is processed
            while (0 < reminderLength)
            {
                // ペイロード読み取り中でない場合は新しいパケットのヘッダを解析
                // If not currently reading payload, parse header for new packet
                if (!_isReadingPayload)
                {
                    // パケット長を取得
                    // Get packet length from header
                    if (TryGetLength(packet, actualStartPacketDataIndex, length, out var payloadLength, out var headerLength))
                    {
                        _packetLength = payloadLength;
                        _isReadingPayload = true;
                        // パケット長の4バイトヘッダを取り除く
                        // Remove the 4-byte header from remaining length
                        reminderLength -= _packetLength + headerLength;
                        actualStartPacketDataIndex += headerLength;
                    }
                    else
                    {
                        // 残りバッファサイズ的に取得できない場合は次回の受信で取得する
                        // Wait for more data in next receive
                        break;
                    }
                }
                else
                {
                    // 前回からの続きのデータがある場合
                    // If continuation data exists from previous call
                    _packetLength -= _nextPacketLengthOffset;
                    reminderLength = length - _packetLength;
                }

                // パケットが切れているので、残りのデータを一時保存
                // Packet is incomplete, save remaining data for next call
                if (reminderLength < 0)
                {
                    // 実際の受信バイト数までのデータのみを保存
                    // Only save data up to actual received byte count
                    var bytesToCopy = length - actualStartPacketDataIndex;
                    for (var i = 0; i < bytesToCopy; i++)
                    {
                        _continuationFromLastTimeBytes.Add(packet[actualStartPacketDataIndex + i]);
                    }
                    // 次回の受信のためにどこからデータを保存するかのオフセットを保存
                    // Save offset for next receive
                    _nextPacketLengthOffset = bytesToCopy;
                    break;
                }

                // パケットの長さ分だけデータを取得
                // Get data for the packet length
                for (var i = 0;
                     i < _packetLength && actualStartPacketDataIndex < length;
                     actualStartPacketDataIndex++, i++)
                    _continuationFromLastTimeBytes.Add(packet[actualStartPacketDataIndex]);

                result.Add(_continuationFromLastTimeBytes);
                // パケット完了、次のパケットに備えてリセット
                // Packet complete, reset for next packet
                _continuationFromLastTimeBytes = new List<byte>();
                _isReadingPayload = false;
            }

            return result;
        }


        private bool TryGetLength(byte[] bytes, int startIndex, int actualLength, out int payloadLength, out int headerLength)
        {
            List<byte> headerBytes;
            if (_isGettingLength)
            {
                // 実際の受信バイト数と必要なヘッダバイト数を比較
                // Compare actual received bytes with required header bytes
                var availableBytes = actualLength - startIndex;
                if (availableBytes < _remainingHeaderLength)
                {
                    // バイト数が足りない場合は読めた分だけ保存して次回に持ち越す
                    // Not enough bytes: save what we can and wait for next receive
                    for (var i = 0; i < availableBytes; i++)
                    {
                        _packetLengthBytes.Add(bytes[startIndex + i]);
                    }
                    _remainingHeaderLength -= availableBytes;
                    payloadLength = -1;
                    headerLength = availableBytes;
                    return false;
                }

                headerLength = _remainingHeaderLength;
                for (var i = 0; i < _remainingHeaderLength; i++) _packetLengthBytes.Add(bytes[startIndex + i]);
                headerBytes = _packetLengthBytes;
                _isGettingLength = false;
            }
            else
            {
                payloadLength = -1;
                headerLength = -1;
                // パケット長が取得できない場合（実際の受信バイト数を使用）
                // If header cannot be fully read (use actual received byte count)
                if (actualLength <= startIndex + 3)
                {
                    _packetLengthBytes.Clear();
                    _remainingHeaderLength = 4;
                    for (var i = startIndex; i < actualLength; i++)
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