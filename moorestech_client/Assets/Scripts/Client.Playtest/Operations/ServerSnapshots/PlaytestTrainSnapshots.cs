using System;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Playtest.Operations
{
    public static class PlaytestTrainSnapshots
    {
        // 時刻表と走行状態を同じtickで読み、可変リストを返さない
        // Read timetable and motion state in one tick without returning mutable lists
        public static async UniTask<(int Entries, bool AutoRun, double Speed)> ReadTrainState(this PlaytestDriver driver, TrainUnit train)
        {
            var state = (Entries: 0, AutoRun: false, Speed: 0d);
            await PlaytestServerThreadAction.RunAsync("列車状態を複製する", () => state = (train.trainDiagram.Entries.Count, train.IsAutoRun, train.CurrentSpeed));
            return state;
        }

        public static async UniTask WaitForTrainState(this PlaytestDriver driver, TrainUnit train, int? entries, bool? autoRun, bool? moving, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                var state = await driver.ReadTrainState(train);
                if ((!entries.HasValue || state.Entries == entries.Value) && (!autoRun.HasValue || state.AutoRun == autoRun.Value) &&
                    (!moving.HasValue || (state.Speed > 0) == moving.Value)) return;
                if (deadline < Time.realtimeSinceStartup) throw new TimeoutException("列車状態の待機が時間切れです");
                await UniTask.Yield();
            }
        }
    }
}
