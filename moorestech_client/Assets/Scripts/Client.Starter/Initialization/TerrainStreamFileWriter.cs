using System;
using System.Collections.Generic;
using System.IO;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// 受信した論理ストリームをセグメント境界で切り分け、元のterrainファイルとして書き戻す
    /// Splits the received logical stream at segment boundaries and writes it back as the original terrain files
    /// </summary>
    public class TerrainStreamFileWriter : IDisposable
    {
        private readonly List<(string FilePath, long ByteLength)> _segments;

        private int _segmentIndex;
        private long _writtenInSegment;
        private FileStream _currentFileStream;

        public TerrainStreamFileWriter(List<(string FilePath, long ByteLength)> segments)
        {
            _segments = segments;
        }

        // ストリームの続きを書き込む。セグメントを跨ぐ場合は境界で自動的にファイルを切り替える
        // Append the next part of the stream, switching files automatically when a segment boundary is crossed
        public void Write(byte[] buffer)
        {
            var writtenInBuffer = 0;
            while (writtenInBuffer < buffer.Length)
            {
                // 全セグメントを書き切った後にまだデータが残るのはサーバーとの長さ不一致
                // Leftover data after every segment is filled means the length contract with the server is broken
                if (_segments.Count <= _segmentIndex)
                    throw new InvalidOperationException("Terrain stream contained more bytes than the expected terrain files.");

                var segment = _segments[_segmentIndex];
                _currentFileStream ??= File.Create(segment.FilePath);

                var writableLength = (int)Math.Min(buffer.Length - writtenInBuffer, segment.ByteLength - _writtenInSegment);
                _currentFileStream.Write(buffer, writtenInBuffer, writableLength);
                writtenInBuffer += writableLength;
                _writtenInSegment += writableLength;

                if (_writtenInSegment < segment.ByteLength) continue;

                CloseCurrentSegment();
            }
        }

        // 全セグメントが想定バイト長ぶん埋まったことを確認する。途中で終わったストリームを正常扱いしない
        // Verify every segment was filled to its expected length so a truncated stream is never treated as success
        public void ThrowIfTruncated()
        {
            if (_segmentIndex == _segments.Count) return;
            throw new InvalidOperationException(
                $"Terrain stream ended early: {_segmentIndex}/{_segments.Count} files completed.");
        }

        public void Dispose()
        {
            _currentFileStream?.Dispose();
            _currentFileStream = null;
        }

        private void CloseCurrentSegment()
        {
            _currentFileStream.Dispose();
            _currentFileStream = null;
            _writtenInSegment = 0;
            _segmentIndex++;
        }
    }
}
