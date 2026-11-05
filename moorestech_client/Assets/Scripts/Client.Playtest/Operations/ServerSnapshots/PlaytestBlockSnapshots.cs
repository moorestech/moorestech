using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.State;
using Client.Playtest.Core;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Playtest.Operations
{
    public static class PlaytestBlockSnapshots
    {
        public static async UniTask<int> ReadWorldBlockCount(this PlaytestDriver driver)
        {
            var count = 0;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "ワールドのブロック数を読む", () => count = ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            return count;
        }

        public static async UniTask<int> ReadItemCount(this PlaytestDriver driver, IOpenableBlockInventoryComponent inventory, ItemId id)
        {
            var count = 0;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "ブロック在庫数を複製する", () =>
            {
                for (var i = 0; i < inventory.GetSlotSize(); i++)
                    if (inventory.GetItem(i).Id == id) count += inventory.GetItem(i).Count;
            });
            return count;
        }

        public static async UniTask WaitForItemCount(this PlaytestDriver driver, IOpenableBlockInventoryComponent inventory, ItemId id, int min, int? max, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var count = await driver.ReadItemCount(inventory, id);
                if (min <= count && (!max.HasValue || count <= max.Value)) { wait.Complete(); return; }
                wait.CheckTimeout($"item={id}, expected min={min}, max={max}, actual={count}");
                await UniTask.Yield();
            }
        }

        public static async UniTask<ProcessState> ReadMachineState(this PlaytestDriver driver, IBlock block)
        {
            var state = ProcessState.Idle;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "機械の加工状態を読む", () => state = ((IMachineProcessStateReader)block.GetComponent<IMachineRecipeSelectorComponent>()).CurrentState);
            return state;
        }

        public static async UniTask WaitForMachineState(this PlaytestDriver driver, IBlock block, ProcessState[] allowed, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var state = await driver.ReadMachineState(block);
                if (allowed.Contains(state)) { wait.Complete(); return; }
                wait.CheckTimeout($"expected states=[{string.Join(",", allowed)}], actual={state}");
                await UniTask.Yield();
            }
        }

        public static async UniTask WaitForItemPrefix(this PlaytestDriver driver, IOpenableBlockInventoryComponent inventory, string prefix, int minimum, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var count = 0;
                await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "対象品の個数を読む", () => count = inventory.InventoryItems
                    .Where(item => item.Count > 0 && MasterHolder.ItemMaster.GetItemMaster(item.Id).Name.StartsWith(prefix)).Sum(item => item.Count));
                if (minimum <= count) { wait.Complete(); return; }
                wait.CheckTimeout($"prefix={prefix}, expected minimum={minimum}, actual={count}");
                await UniTask.Yield();
            }
        }

        // segmentとtickの可変値は参照を持ち出さず数値へ写す
        // Copy mutable segment and tick values into numbers without leaking references
        public static async UniTask<int> ReadBeltSegmentCount(this PlaytestDriver driver)
        {
            var count = 0;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "ベルトsegment数を読む", () => count = ServerContext.GetService<BeltTransportDatastore>().Assembly.Segments.Length);
            return count;
        }

        public static async UniTask<uint> ReadTrainTick(this PlaytestDriver driver)
        {
            uint tick = 0;
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "サーバー列車tickを読む", () => tick = ServerContext.GetService<TrainUpdateService>().GetCurrentTick());
            return tick;
        }
    }
}
