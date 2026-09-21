using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.Common
{
    internal static class ShutdownFlushResultAggregator
    {
        internal static ShutdownFlushResult AggregateAndReport(ShutdownFlushResult[] results)
        {
            var aggregated = AggregateByPriority();
            ReportMaskedFailures();
            return aggregated;

            #region Internal

            ShutdownFlushResult AggregateByPriority()
            {
                // 諦めは上限到達より重い。世界が保存されていない事実は待ち切れなかった事実に埋もれてはいけない
                // A give-up outweighs a timeout: an unsaved world must not be hidden behind "did not finish waiting"
                foreach (var result in results)
                    if (result == ShutdownFlushResult.SaveAbandoned)
                        return ShutdownFlushResult.SaveAbandoned;

                // 書き出しが失敗した参加者は「書けた」と名乗れない。正常値のNothingFlushedへ潰すと、保存されていない世界がFlushedとして閉じる
                // A participant whose flush failed cannot claim success; folding it into the normal NothingFlushed would close an unsaved world as Flushed
                foreach (var result in results)
                    if (result == ShutdownFlushResult.FlushFailed)
                        return ShutdownFlushResult.FlushFailed;

                // 1つでも書き切れていなければ全体を上限到達として返す
                // Report the whole flush as timed out if any single participant failed to finish
                foreach (var result in results)
                    if (result == ShutdownFlushResult.FlushTimedOut)
                        return ShutdownFlushResult.FlushTimedOut;
                return ShutdownFlushResult.Flushed;
            }

            // 例外と上限到達が同時に起きると、戻り値に残らなかった側は QuitApplicationAsync のログにも出ない。種類ごとに1度だけ事実を残す
            // When an exception and a timeout happen together, the one the return value dropped never reaches QuitApplicationAsync's log, so each kind is stated once here
            void ReportMaskedFailures()
            {
                var reported = new List<ShutdownFlushResult>();
                foreach (var result in results)
                {
                    if (result == aggregated || result == ShutdownFlushResult.Flushed) continue;
                    if (result == ShutdownFlushResult.NothingFlushed || result == ShutdownFlushResult.AlreadyShutdown) continue;
                    if (reported.Contains(result)) continue;
                    reported.Add(result);
                    Debug.LogError($"終了時の書き出しで別の失敗も同時に起きています（戻り値は {aggregated} に畳まれます）: {result}");
                }
            }

            #endregion
        }
    }
}
