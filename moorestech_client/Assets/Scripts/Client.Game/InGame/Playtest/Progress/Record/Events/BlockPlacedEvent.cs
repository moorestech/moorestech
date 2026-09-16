using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // 区間の設置数の合計。1件ずつ残さず区間の合計だけを載せる（ADR 0060 裁定9）
    // The interval's placement total, carried as one sum instead of one line per placement (ADR 0060 adjudication 9)
    internal sealed class BlockPlacedEvent : IProgressEvent
    {
        public const string TypeName = "blockPlaced";
        private const string CountKey = "count";

        public string T { get; }
        public ulong Tick { get; }
        public int Count { get; }

        public BlockPlacedEvent(DateTime utc, ulong tick, int count) : this(ProgressUtcTime.ToIso(utc), tick, count)
        {
        }

        private BlockPlacedEvent(string t, ulong tick, int count)
        {
            T = t;
            Tick = tick;
            Count = count;
        }

        public static BlockPlacedEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadInt(data, CountKey, TypeName, out var count) ? new BlockPlacedEvent(t, tick, count) : null;
        }

        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
            aggregate.AddPlacedBlocks(Count);
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [CountKey] = Count });
        }
    }
}
