using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // UI状態の遷移。集計では最後の状態になり、建築モードの離脱の合成もこれを起点にする
    // A UI state transition; it becomes the last state in the aggregate and anchors the build-mode cancel synthesis
    internal sealed class UiStateChangedEvent : IProgressEvent
    {
        public const string TypeName = "uiStateChanged";
        private const string StateKey = "state";

        public string T { get; }
        public ulong Tick { get; }
        public string State { get; }

        public UiStateChangedEvent(DateTime utc, ulong tick, string state) : this(ProgressUtcTime.ToIso(utc), tick, state)
        {
        }

        private UiStateChangedEvent(string t, ulong tick, string state)
        {
            T = t;
            Tick = tick;
            State = state;
        }

        public static UiStateChangedEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadString(data, StateKey, TypeName, out var state) ? new UiStateChangedEvent(t, tick, state) : null;
        }

        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
            aggregate.SetLastUiState(State);
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [StateKey] = State });
        }
    }
}
