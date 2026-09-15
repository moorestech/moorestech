using System;
using Client.Game.InGame.BugReport.Playtest;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // プレイ報告の送信。購読で観測できない操作なので、送信成功の直後にプッシュされる
    // A play report sent; no subscription observes it, so it is pushed right after a successful send
    internal sealed class ReportSentEvent : IProgressEvent
    {
        public const string TypeName = "reportSent";
        private const string KindKey = "kind";

        public string T { get; }
        public ulong Tick { get; }
        public PlaytestReportKind Kind { get; }

        public ReportSentEvent(DateTime utc, ulong tick, PlaytestReportKind kind) : this(ProgressUtcTime.ToIso(utc), tick, kind)
        {
        }

        private ReportSentEvent(string t, ulong tick, PlaytestReportKind kind)
        {
            T = t;
            Tick = tick;
            Kind = kind;
        }

        public static ReportSentEvent FromData(string t, ulong tick, JObject data)
        {
            if (!ProgressEventLine.TryReadString(data, KindKey, TypeName, out var kindText)) return null;
            if (PlaytestReportKindText.TryParse(kindText, out var kind)) return new ReportSentEvent(t, tick, kind);
            Debug.LogWarning($"進行記録の報告送信の種別が契約値でないため飛ばします kind:{kindText}");
            return null;
        }

        // 送信は集計値を持たない。記録の events 列にだけ現れる
        // A send carries no aggregate; it appears only in the record's events column
        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [KindKey] = PlaytestReportKindText.ToContractText(Kind) });
        }
    }
}
