using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // 設置せずに建築モードを抜けた離脱。後段の合成なので時刻を持たず、離脱を起こした遷移の時刻とtickを引き継ぐ
    // Leaving build mode without placing anything; synthesized afterwards, it owns no clock and inherits the causing transition's time and tick
    internal sealed class BuildModeCancelledEvent : IProgressEvent
    {
        public const string TypeName = "buildModeCancelled";
        private const string NextStateKey = "nextState";

        public string T { get; }
        public ulong Tick { get; }
        public string NextState { get; }

        private BuildModeCancelledEvent(string t, ulong tick, string nextState)
        {
            T = t;
            Tick = tick;
            NextState = nextState;
        }

        public static BuildModeCancelledEvent CausedBy(UiStateChangedEvent transition)
        {
            return new BuildModeCancelledEvent(transition.T, transition.Tick, transition.State);
        }

        public static BuildModeCancelledEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadString(data, NextStateKey, TypeName, out var nextState) ? new BuildModeCancelledEvent(t, tick, nextState) : null;
        }

        // 離脱は集計値を持たない。記録の events 列にだけ現れる
        // A cancel carries no aggregate; it appears only in the record's events column
        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [NextStateKey] = NextState });
        }
    }
}
