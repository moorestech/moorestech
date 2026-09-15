using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // チャレンジの到達。baseline と同じ到達が重ねて届いても集計側の集合が二重計上を防ぐ
    // A challenge reached; the aggregate's set keeps a reach already in the baseline from counting twice
    internal sealed class ChallengeCompletedEvent : IProgressEvent
    {
        public const string TypeName = "challengeCompleted";
        private const string ChallengeGuidKey = "challengeGuid";

        public string T { get; }
        public ulong Tick { get; }
        public string ChallengeGuid { get; }

        public ChallengeCompletedEvent(DateTime utc, ulong tick, string challengeGuid) : this(ProgressUtcTime.ToIso(utc), tick, challengeGuid)
        {
        }

        private ChallengeCompletedEvent(string t, ulong tick, string challengeGuid)
        {
            T = t;
            Tick = tick;
            ChallengeGuid = challengeGuid;
        }

        public static ChallengeCompletedEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadString(data, ChallengeGuidKey, TypeName, out var challengeGuid) ? new ChallengeCompletedEvent(t, tick, challengeGuid) : null;
        }

        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
            aggregate.AddReachedChallenge(ChallengeGuid);
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [ChallengeGuidKey] = ChallengeGuid });
        }
    }
}
