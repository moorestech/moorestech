using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点在庫のロード時復元。縮小スロットの残存品検出と、昇降中の積荷の戻し先を受け持つ
    // Load-time restoration of the base inventory: detects items in truncated slots and places returned climbing payloads
    internal static class SpaceElevatorInventoryRestorer
    {
        // 縮小したスロットへのロードは、何も確定する前に残存品を検出する
        // Detect retained items in truncated slots before committing any loaded inventory
        internal static void Restore(IReadOnlyList<ItemStackSaveJsonObject> items, OpenableInventoryItemDataStoreService inventory, int slotCount, BlockInstanceId instanceId)
        {
            for (var i = slotCount; i < items.Count; i++)
            {
                // silent-ok: 空の末尾には保存すべき品物が無い / 残る在庫はロード後のUIで見える
                if (items[i].Count == 0) continue;
                var reason = $"[SpaceElevatorBase] 出口縮小で非空スロット {i} を失うためロードを中断します BlockInstanceId:{instanceId} count:{items[i].Count}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            // 全品が収まることを確かめてから復元する
            // Restore only after verifying every saved item can be retained
            for (var i = 0; i < slotCount && i < items.Count; i++) inventory.SetItemWithoutEvent(i, items[i].ToItemStack());
        }

        // ロード時に昇降中の積荷を1便ぶんへ正規化する。昇らせない分は入口・出口へ戻し、入らなければ消す
        // Normalize a climbing payload to one launch on load; return the rest to the inlet or outlets and discard what does not fit
        internal static IItemStack TakeLaunchableClimbingPayloadOnLoad(IItemStack savedPayload, IReadOnlyDictionary<ItemId, int> countPerLaunchByCarriedPayload, OpenableInventoryItemDataStoreService inventory, IReadOnlyList<int> outputSlots, BlockInstanceId instanceId)
        {
            var factory = ServerContext.ItemStackFactory;

            // 運ぶ積荷から外れた積荷は頂上で軌道到着サービスが例外を出すので、昇降を取り消して全量を戻す
            // A payload no longer carried would make the orbital service throw at the top, so cancel the climb and return all of it
            if (!countPerLaunchByCarriedPayload.TryGetValue(savedPayload.Id, out var countPerLaunch))
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星 {savedPayload.Id} は今のマスタで運ぶ積荷ではないため、昇降を取り消して入口へ戻します BlockInstanceId:{instanceId}");
                ReturnPayloadOnLoadWithoutEvent(savedPayload);
                return factory.CreatEmpty();
            }

            // 1便に足りない積荷は頂上で例外になるので、昇降を取り消して全量を戻す
            // A short payload would throw at the top, so cancel the climb and return all of it
            if (savedPayload.Count < countPerLaunch)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の積荷 {savedPayload.Id} が {savedPayload.Count} 個で1便の {countPerLaunch} 個に足りないため、昇降を取り消して入口へ戻します BlockInstanceId:{instanceId}");
                ReturnPayloadOnLoadWithoutEvent(savedPayload);
                return factory.CreatEmpty();
            }

            // 1便より多いときは1便ぶんだけ昇らせ、余りを戻す
            // With more than one launch, keep one launch climbing and return the rest
            if (countPerLaunch < savedPayload.Count)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の積荷が {savedPayload.Count} 個セーブにあるため、1便の {countPerLaunch} 個だけ昇らせ残りを入口へ戻します BlockInstanceId:{instanceId}");
                ReturnPayloadOnLoadWithoutEvent(factory.Create(savedPayload.Id, savedPayload.Count - countPerLaunch));
                return factory.Create(savedPayload.Id, countPerLaunch);
            }
            return savedPayload;

            #region Internal

            // 入口→出口の空きの順に入れる。セーブを読めなくするより積荷を消すほうがよいので、どこにも入らない分は消す
            // Fill the inlet then free outlet space; losing the payload beats an unloadable save, so discard what fits nowhere
            void ReturnPayloadOnLoadWithoutEvent(IItemStack payload)
            {
                var afterInlet = AddWithoutEvent(SpaceElevatorBaseInventoryComponent.SatelliteSlot, payload);
                // silent-ok: 入口へ全量戻せた正常な復元 / 呼び出し元が昇降の取消をLogErrorで出している
                if (afterInlet.Count == 0) return;
                var inlet = inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot);
                var inletReason = inlet.Id == payload.Id ? "入口が満杯の" : $"入口が別の積荷 {inlet.Id} で埋まっている";

                // 入口に入らない分を出口の空きへ退避する
                // Move what the inlet cannot hold into free outlet space
                var afterOutlets = afterInlet;
                for (var i = 0; i < outputSlots.Count && 0 < afterOutlets.Count; i++) afterOutlets = AddWithoutEvent(outputSlots[i], afterOutlets);
                var evacuatedCount = afterInlet.Count - afterOutlets.Count;
                if (0 < evacuatedCount) Debug.LogError($"[SpaceElevatorBase] {inletReason}ため、ロード時に戻す積荷 item:{payload.Id} count:{evacuatedCount} を出口へ退避します BlockInstanceId:{instanceId}");
                if (afterOutlets.Count == 0) return;
                Debug.LogError($"[SpaceElevatorBase] {inletReason}うえ出口も満杯のため、ロード時に戻せない積荷 item:{payload.Id} count:{afterOutlets.Count} を消します BlockInstanceId:{instanceId}");
            }

            IItemStack AddWithoutEvent(int slot, IItemStack payload)
            {
                var result = inventory.GetItem(slot).AddItem(payload);
                inventory.SetItemWithoutEvent(slot, result.ProcessResultItemStack);
                return result.RemainderItemStack;
            }

            #endregion
        }
    }
}
