using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest.Core;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using UnityEngine;

namespace Client.Playtest.Operations
{
    public static class PlaytestOrbitSnapshots
    {
        public static async UniTask<IReadOnlyList<IItemStack>> ReadArrivalOutputs(this PlaytestDriver driver, ItemId payloadId, int count)
        {
            IReadOnlyList<IItemStack> outputs = null;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "到着成果を複製する", () =>
                outputs = ServerContext.GetService<IOrbitalArrivalService>().PreviewArrivalOutputs(payloadId, count).ToArray());
            return outputs;
        }

        // プールの可変値をtick内で読み、待機はメインスレッドで進める
        // Read the mutable pool inside the tick and drive waiting on the main thread
        public static async UniTask WaitForCableCount(this PlaytestDriver driver, int expected, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var amount = 0;
                await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "ケーブル残量を読む", () => amount = ServerContext.GetService<SpaceElevatorCablePool>().Amount);
                if (amount == expected) { wait.Complete(); return; }
                wait.CheckTimeout($"cables expected={expected}, actual={amount}");
                await UniTask.Yield();
            }
        }
    }
}
