using Client.Localization;
using Mooresmaster.Localization.Generated;
using TMPro;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ローディング画面へ進捗1行を追記する。経過時間の書式と位置パラメータ順をここだけが知る
    /// Appends one progress line to the loading screen; only this type knows the elapsed format and parameter order
    /// </summary>
    public class LoadingProgressLog
    {
        private readonly TMP_Text _loadingLog;
        private readonly System.Diagnostics.Stopwatch _loadingStopwatch;

        public LoadingProgressLog(TMP_Text loadingLog, System.Diagnostics.Stopwatch loadingStopwatch)
        {
            _loadingLog = loadingLog;
            _loadingStopwatch = loadingStopwatch;
        }

        public void AppendElapsed(LocalizationKey key)
        {
            Append(Localize.GetFormatted(key, new[] { _loadingStopwatch.Elapsed.ToString() }));
        }

        // 経過時間の前に1つだけ値を差し込む行のための経路（地形の取得チャンク数）
        // Path for lines that carry one value ahead of the elapsed time, such as the fetched chunk count
        public void AppendElapsed(LocalizationKey key, string leadingParam)
        {
            Append(Localize.GetFormatted(key, new[] { leadingParam, _loadingStopwatch.Elapsed.ToString() }));
        }

        public void Append(LocalizationKey key)
        {
            Append(Localize.Get(key));
        }

        private void Append(string line)
        {
            _loadingLog.text += "\n" + line;
        }
    }
}
