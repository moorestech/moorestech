using System;
using Client.Game.InGame.Playtest.Progress;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    // 進行記録が登録されていない構成向けの縮退実装。記録は落ちるが、クラフトも報告送信も操作自体は通す
    // The degraded implementation for a configuration without the progress recorder; recording is lost while crafting and reporting still work
    // 縮退は無音にしない。1度だけ理由を出し、以降の呼び出しでログを埋め尽くさない
    // The degradation is never silent: the reason is logged once, and later calls do not flood the log
    public sealed class NullPlaytestProgressSink : IPlaytestProgressSink
    {
        public static readonly NullPlaytestProgressSink Instance = new();

        private bool _reported;

        public void RecordCraftRequested(Guid recipeGuid)
        {
            ReportOnce();
        }

        public void RecordReportSent(string kind)
        {
            ReportOnce();
        }

        private void ReportOnce()
        {
            if (_reported) return;
            _reported = true;
            Debug.LogError("進行記録の窓口(IPlaytestProgressSink)が登録されていないため、このセッションの操作は進行記録へ残りません");
        }
    }
}
