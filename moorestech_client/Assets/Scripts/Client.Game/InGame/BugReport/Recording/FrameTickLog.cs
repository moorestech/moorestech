using System.Collections.Generic;
using System.Text;

namespace Client.Game.InGame.BugReport.Recording
{
    // 取り込んだフレームの時刻とサーバーtickの対応。動画のフレームをパケットログと同じ軸で読むために使う
    // Maps captured frames' clock time to the server tick so video frames can be read on the packet log's axis
    public sealed class FrameTickLog
    {
        public const int Capacity = 1200;

        private readonly object _lock = new();
        private readonly Queue<(long unixMs, ulong tick)> _rows = new(Capacity + 1);

        public void Add(long unixMs, ulong tick)
        {
            lock (_lock)
            {
                _rows.Enqueue((unixMs, tick));
                if (_rows.Count > Capacity) _rows.Dequeue();
            }
        }

        public IReadOnlyList<(long unixMs, ulong tick)> Dump()
        {
            lock (_lock)
            {
                return new List<(long unixMs, ulong tick)>(_rows);
            }
        }

        public static string ToTsv(IReadOnlyList<(long unixMs, ulong tick)> rows)
        {
            var builder = new StringBuilder("unixMs\ttick\n");
            foreach (var (unixMs, tick) in rows) builder.Append(unixMs).Append('\t').Append(tick).Append('\n');
            return builder.ToString();
        }
    }
}
