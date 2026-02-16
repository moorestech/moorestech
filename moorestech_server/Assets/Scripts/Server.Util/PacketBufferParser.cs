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
        private readonly byte[] _packetLengthBytes = new byte[4];
        private int _packetLengthBytesCount;
        private byte[] _continuationFromLastTimeBytes;
        private int _continuationWriteOffset;

        private bool _isGettingLength;
        private bool _isReadingPayload;
        private int _packetLength;

        public List<byte[]> Parse(byte[] packet, int length)
        {
            // プロトコル長から実際のプロトコルを作る
            // Parse actual protocol data from buffer
            var actualStartPacketDataIndex = 0;
            var reminderLength = length;

            var result = new List<byte[]>();

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
                        // 不正なペイロード長を検出した場合はリセット
                        // Reset if payload length is invalid
                        if (payloadLength <= 0)
                        {
                            _isReadingPayload = false;
                            break;
                        }

                        _packetLength = payloadLength;
                        _isReadingPayload = true;
                        // ヘッダー解析後にバッファを事前確保
                        // Pre-allocate buffer after header parsing
                        _continuationFromLastTimeBytes = new byte[_packetLength];
                        _continuationWriteOffset = 0;
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
                    var remaining = _packetLength - _continuationWriteOffset;
                    reminderLength = length - remaining;
                }

                // パケットが切れているので、残りのデータを一時保存
                // Packet is incomplete, save remaining data for next call
                if (reminderLength < 0)
                {
                    // 実際の受信バイト数までのデータのみを保存
                    // Only save data up to actual received byte count
                    var bytesToCopy = length - actualStartPacketDataIndex;
                    Array.Copy(packet, actualStartPacketDataIndex, _continuationFromLastTimeBytes, _continuationWriteOffset, bytesToCopy);
                    _continuationWriteOffset += bytesToCopy;
                    break;
                }

                // パケットの長さ分だけデータを取得
                // Get data for the packet length
                var copyLength = _packetLength - _continuationWriteOffset;
                if (actualStartPacketDataIndex + copyLength > length)
                    copyLength = length - actualStartPacketDataIndex;
                Array.Copy(packet, actualStartPacketDataIndex, _continuationFromLastTimeBytes, _continuationWriteOffset, copyLength);
                actualStartPacketDataIndex += copyLength;

                result.Add(_continuationFromLastTimeBytes);
                // パケット完了、次のパケットに備えてリセット
                // Packet complete, reset for next packet
                _continuationFromLastTimeBytes = null;
                _continuationWriteOffset = 0;
                _isReadingPayload = false;
            }

            return result;
        }


        private bool TryGetLength(byte[] bytes, int startIndex, int actualLength, out int payloadLength, out int headerLength)
        {
            if (_isGettingLength)
            {
                // 実際の受信バイト数と必要なヘッダバイト数を比較
                // Compare actual received bytes with required header bytes
                var availableBytes = actualLength - startIndex;
                var remainingHeader = 4 - _packetLengthBytesCount;
                if (availableBytes < remainingHeader)
                {
                    // バイト数が足りない場合は読めた分だけ保存して次回に持ち越す
                    // Not enough bytes: save what we can and wait for next receive
                    Array.Copy(bytes, startIndex, _packetLengthBytes, _packetLengthBytesCount, availableBytes);
                    _packetLengthBytesCount += availableBytes;
                    payloadLength = -1;
                    headerLength = availableBytes;
                    return false;
                }

                headerLength = remainingHeader;
                Array.Copy(bytes, startIndex, _packetLengthBytes, _packetLengthBytesCount, remainingHeader);
                _packetLengthBytesCount = 4;
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
                    _packetLengthBytesCount = 0;
                    for (var i = startIndex; i < actualLength; i++)
                    {
                        _packetLengthBytes[_packetLengthBytesCount] = bytes[i];
                        _packetLengthBytesCount++;
                    }

                    _isGettingLength = true;
                    return false;
                }

                headerLength = 4;
                _packetLengthBytes[0] = bytes[startIndex];
                _packetLengthBytes[1] = bytes[startIndex + 1];
                _packetLengthBytes[2] = bytes[startIndex + 2];
                _packetLengthBytes[3] = bytes[startIndex + 3];
                _packetLengthBytesCount = 4;
            }

            // ビッグエンディアンからリトルエンディアンに変換（ローカルコピーで行う）
            // Convert from big-endian to little-endian (using local copy)
            var lengthBytes = new byte[4];
            Array.Copy(_packetLengthBytes, lengthBytes, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            payloadLength = BitConverter.ToInt32(lengthBytes, 0);
            return true;
        }
    }
}
