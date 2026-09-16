using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // 研究の完了。baseline と同じ研究が重ねて届いても集計側の集合が二重計上を防ぐ
    // A research completed; the aggregate's set keeps one already in the baseline from counting twice
    internal sealed class ResearchCompletedEvent : IProgressEvent
    {
        public const string TypeName = "researchCompleted";
        private const string ResearchGuidKey = "researchGuid";

        public string T { get; }
        public ulong Tick { get; }
        public string ResearchGuid { get; }

        public ResearchCompletedEvent(DateTime utc, ulong tick, string researchGuid) : this(ProgressUtcTime.ToIso(utc), tick, researchGuid)
        {
        }

        private ResearchCompletedEvent(string t, ulong tick, string researchGuid)
        {
            T = t;
            Tick = tick;
            ResearchGuid = researchGuid;
        }

        public static ResearchCompletedEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadString(data, ResearchGuidKey, TypeName, out var researchGuid) ? new ResearchCompletedEvent(t, tick, researchGuid) : null;
        }

        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
            aggregate.AddCompletedResearch(ResearchGuid);
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [ResearchGuidKey] = ResearchGuid });
        }
    }
}
