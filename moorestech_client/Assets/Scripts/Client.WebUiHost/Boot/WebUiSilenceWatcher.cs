using System;
using System.Diagnostics;
using Client.Input;
using UniRx;
using VContainer.Unity;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// 1秒ごとにWS無応答を評価
    /// Evaluates WS silence every second
    /// </summary>
    public sealed class WebUiSilenceWatcher : IInitializable
    {
        // 無応答は「何も来ない」ことなので購読できる変化が無く、時計で見る
        // Silence means nothing arrives, so there is no change to subscribe to and a clock watches instead
        public void Initialize()
        {
            Observable.Interval(TimeSpan.FromSeconds(1), Scheduler.MainThreadIgnoreTimeScale).Subscribe(_ => WebUiInputExclusivity.EvaluateWebSilence(Stopwatch.GetTimestamp()));
        }
    }
}
