using Client.Game.InGame.Playtest.Progress;
using UnityEngine;

namespace Client.Starter.Registration
{
    /// <summary>
    /// 記録を集めない起動の進行記録の窓口。報告actionは窓口を必ず受け取るため、記録しない実装をここで渡す。
    /// The progress window for a boot that collects nothing; the report action always takes a window, so this non-recording one is handed over.
    /// </summary>
    internal sealed class UncollectedPlaytestProgressSink : IPlaytestProgressSink
    {
        // 記録しないことを無音にしない。この起動では進行記録そのものが無いことをログへ残す
        // Not recording is never silent: the log states this boot keeps no progress record at all
        public void RecordReportSent(string kind)
        {
            Debug.Log($"UncollectedPlaytestProgressSink: この起動はプレイテストの記録を集めないため、報告の送信を進行記録へ残しません kind:{kind}");
        }
    }
}
