using System;
using Client.Playtest.Core;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.CleanRoom;
using Game.Context;
using UnityEngine;

namespace Client.Playtest.Operations
{
    public static class PlaytestCleanRoomSnapshots
    {
        public static async UniTask WaitForCleanRoomClass(this PlaytestDriver driver, IBlock block, int threshold, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var ready = false;
                await PlaytestServerThreadAction.RunAsync("清浄室のクラスを読む", () =>
                    ready = ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(block, out var room) && room.ThresholdIndex == threshold);
                if (ready) { wait.Complete(); return; }
                wait.CheckTimeout();
                await UniTask.Yield();
            }
        }

        // 部屋オブジェクトを外へ持ち出さず、検出とクラスの結果だけを返す
        // Return only detection/classification results without leaking mutable room objects
        public static async UniTask WaitForCleanRoom(this PlaytestDriver driver, IBlock block, bool requireClass, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var ready = false;
                await PlaytestServerThreadAction.RunAsync("清浄室の検出とクラスを読む", () =>
                    ready = ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(block, out var room) &&
                        (!requireClass || room.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex));
                if (ready) { wait.Complete(); return; }
                wait.CheckTimeout();
                await UniTask.Yield();
            }
        }
    }
}
