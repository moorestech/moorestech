using Client.Localization;
using Mooresmaster.Localization.Generated;
using TMPro;

namespace Client.Starter.Initialization.Progress
{
    /// <summary>
    /// 進捗1行の書式と引数順を集約
    /// Owns the format and argument order of one progress line
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

        // 先頭に値を1つ足す行の経路
        // Path for lines with one leading value
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
