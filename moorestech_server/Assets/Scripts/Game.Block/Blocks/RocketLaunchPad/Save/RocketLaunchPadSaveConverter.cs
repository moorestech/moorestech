using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    // 発射台のセーブ形と段階の型を相互に変換する。壊れた値は段階を作る前に弾き、警告して待機へ戻す
    // Converts between the save shape and stage types; broken values are rejected before building a stage and fall back to idle with a warning
    internal static class RocketLaunchPadSaveConverter
    {
        public static RocketLaunchPadSaveJsonObject Capture(RocketLaunchPadStage stage, int nextPayloadIndex, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            var cargo = stage is LoadedRocketStage loaded ? loaded.Cargo : new RocketCargo(ServerContext.ItemStackFactory.CreatEmpty(), new List<IItemStack>());
            var items = slots.InventoryItems;
            return new RocketLaunchPadSaveJsonObject
            {
                Phase = (int)stage.Phase,
                RemainingSeconds = GameUpdater.TicksToSeconds(RemainingTicksOf()),
                CargoPayload = new ItemStackSaveJsonObject(cargo.Payload),
                CargoMaterials = cargo.Materials.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                NextPayloadIndex = nextPayloadIndex,
                InputItems = items.Take(layout.OutputSlotStart).Where(item => 0 < item.Count).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputItems = items.Skip(layout.OutputSlotStart).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };

            #region Internal
            uint RemainingTicksOf() => stage is IProgressingStage progressing ? progressing.RemainingTicks : 0;
            #endregion
        }

        public static void RestoreSlots(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            // 実在品を保持できる場合だけ復元
            // Restore slots only after confirming that all existing items can be retained
            ValidateStructure(saveData.InputItems, "inputItems");
            ValidateStructure(saveData.OutputItems, "outputItems");
            RocketLaunchPadRetainedItemValidator.Validate(saveData, layout);
            long droppedCount = 0;
            var invalidRecords = 0;
            var recoveredRecords = 0;
            foreach (var input in saveData.InputItems)
            {
                var id = Guid.TryParse(input.ItemGuidStr, out var inputGuid) ? MasterHolder.ItemMaster.GetItemIdOrNull(inputGuid) : null;
                if (id.HasValue && layout.TryGetInputSlot(id.Value, out var slot) && IsValidSlot(input))
                    slots.SetItemWithoutEvent(slot, input.ToItemStack());
                else if (0 < input.Count && CanCreate(input)) continue; // silent-ok: 回収品は出力復元後に保持 / 下の回収ループで出力欄へ戻す
                else CountDiscarded(input);
            }
            for (var i = 0; i < saveData.OutputItems.Count; i++)
            {
                var output = saveData.OutputItems[i];
                if (i < layout.OutputSlotCount && IsValidSlot(output)) slots.SetItemWithoutEvent(layout.OutputSlots[i], output.ToItemStack());
                else if (0 < output.Count && CanCreate(output))
                {
                    RocketLaunchPadCargoRecovery.ReturnItem(output, layout, slots);
                    recoveredRecords++;
                }
                else CountDiscarded(output);
            }
            foreach (var input in saveData.InputItems)
                if (0 < input.Count && CanCreate(input) && !layout.TryGetInputSlot(input.ToItemStack().Id, out _))
                {
                    RocketLaunchPadCargoRecovery.ReturnItem(input, layout, slots);
                    recoveredRecords++;
                }
            if (0 < recoveredRecords) Debug.LogWarning($"発射台の入れ口から外れた品または超過出力を回収欄へ保持しました 件数:{recoveredRecords}");
            if (0 < droppedCount || 0 < invalidRecords)
                Debug.LogWarning($"発射台のスロット構成または保存値が無効なため、復元できない品を {droppedCount} 個捨てました 不正レコード:{invalidRecords} 件");

            #region Internal

            // 構造欠損は部分復元を始める前に明示拒否する
            // Reject structural corruption explicitly before restoring any slots
            void ValidateStructure(List<ItemStackSaveJsonObject> items, string field)
            {
                var reason = items == null ? field : items.Any(item => item == null) ? field + "のnull要素" : null;
                if (reason == null) return;
                var message = $"発射台のセーブの{reason}が欠損しているためロードを拒否します";
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            bool IsValidSlot(ItemStackSaveJsonObject item)
                => CanCreate(item) && (item.Count != 0 || Guid.Parse(item.ItemGuidStr) == Guid.Empty);

            // 実物の損失と不正な保存レコードを分けて数える
            // Count physical item losses separately from invalid saved records
            void CountDiscarded(ItemStackSaveJsonObject item)
            {
                if (0 < item.Count) droppedCount += item.Count;
                if (!IsValidSlot(item)) invalidRecords++;
            }

            #endregion
        }

        // 実在の積荷を持つ段階の集合。検証と復元がこの1か所を見る
        // The set of phases that hold real cargo; validation and restore both read this one place
        internal static bool HoldsCargo(RocketLaunchPadPhase phase)
        {
            return phase is RocketLaunchPadPhase.Building or RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.ReadyToLaunch;
        }

        // 保持するスタックだけが現在の上限内で生成可能かを調べる
        // Check whether a retained stack can be created under the current limit
        internal static bool CanCreate(ItemStackSaveJsonObject item)
        {
            if (!Guid.TryParse(item.ItemGuidStr, out var guid)) return false;
            if (item.Count == 0) return true;
            var id = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
            return 0 < item.Count && id.HasValue && item.Count <= ServerContext.GetService<IItemStackLevelLookup>().GetMaxStack(id.Value);
        }
    }
}
