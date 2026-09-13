using System.Collections.Generic;
using System.Text;

namespace Client.Game.InGame.BugReport.Recording
{
    // 動画へ実際に入った1フレームの目印。区間と枚数を持ち、剪定後も動画のどのフレームがどのtickかを辿れる
    // Marks one frame that really entered the video; the segment and index keep frame-to-tick traceable after pruning
    public readonly struct FrameTickRow
    {
        public long UnixMs { get; }
        public ulong Tick { get; }
        public int SegmentId { get; }
        public int FrameIndex { get; }

        public FrameTickRow(long unixMs, ulong tick, int segmentId, int frameIndex)
        {
            UnixMs = unixMs;
            Tick = tick;
            SegmentId = segmentId;
            FrameIndex = frameIndex;
        }
    }

    // 取り込んだフレームの時刻とサーバーtickの対応。動画のフレームをパケットログと同じ軸で読むために使う
    // Maps captured frames' clock time to the server tick so video frames can be read on the packet log's axis
    public sealed class FrameTickLog
    {
        // 保持行数は録画の保持窓と同じ枚数（RetentionSeconds 120秒 × Fps 10）。動画より長い対応表を残さない
        // The row count matches the recording's retention window (120s x 10fps) so the table never outlives the video
        public const int Capacity = 1200;

        private readonly object _lock = new();
        private readonly Queue<FrameTickRow> _rows = new(Capacity + 1);

        public void Add(long unixMs, ulong tick, int segmentId, int frameIndex)
        {
            lock (_lock)
            {
                _rows.Enqueue(new FrameTickRow(unixMs, tick, segmentId, frameIndex));
                if (_rows.Count > Capacity) _rows.Dequeue();
            }
        }

        public IReadOnlyList<FrameTickRow> Dump()
        {
            lock (_lock)
            {
                return new List<FrameTickRow>(_rows);
            }
        }

        public static string ToTsv(IReadOnlyList<FrameTickRow> rows)
        {
            var builder = new StringBuilder("unixMs\ttick\tsegment\tframe\n");
            foreach (var row in rows)
            {
                builder.Append(row.UnixMs).Append('\t').Append(row.Tick).Append('\t').Append(row.SegmentId).Append('\t').Append(row.FrameIndex).Append('\n');
            }
            return builder.ToString();
        }
    }
}
