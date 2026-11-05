using System;
using System.Diagnostics;
using Client.Input;
using UniRx;
using VContainer.Unity;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// 1秒ごとにWSの無応答を評価し、入力排他のWeb側の値を読むかを決め直させる（§3.11）
    /// Evaluates WS silence every second so input exclusivity re-decides whether to read the web side (§3.11)
    /// </summary>
    public sealed class WebUiSilenceWatcher : IInitializable
    {
        // 無応答は「何も来ない」ことなので購読できる変化が無く、時計で見る（D-10）
        // Silence means nothing arrives, so there is no change to subscribe to and a clock watches instead (D-10)
        public void Initialize()
        {
            Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(_ => UiInputExclusivity.EvaluateWebSilence(Stopwatch.GetTimestamp()));
        }
    }
}
