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
                OutputItems = layout.OutputSlots.Select(slot => new ItemStackSaveJsonObject(items[slot])).ToList(),
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
            foreach (var input in saveData.InputItems)
            {
                var id = Guid.TryParse(input.ItemGuidStr, out var inputGuid) ? MasterHolder.ItemMaster.GetItemIdOrNull(inputGuid) : null;
                if (id.HasValue && layout.TryGetInputSlot(id.Value, out var slot) && IsValidSlot(input))
                    slots.SetItemWithoutEvent(slot, input.ToItemStack());
                else CountDiscarded(input);
            }
            for (var i = 0; i < saveData.OutputItems.Count; i++)
            {
                var output = saveData.OutputItems[i];
                if (i < layout.OutputSlotCount && IsValidSlot(output)) slots.SetItemWithoutEvent(layout.OutputSlots[i], output.ToItemStack());
                else CountDiscarded(output);
            }
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

        public static RocketLaunchPadStage RestoreStage(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout)
        {
            if (!saveData.Phase.HasValue || !Enum.IsDefined(typeof(RocketLaunchPadPhase), saveData.Phase.Value))
                return FallBackToIdle($"段階の値が不明です 段階:{saveData.Phase}");
            if (saveData.CargoPayload == null) return FallBackToIdle("cargoPayloadが欠損しています");
            if (saveData.CargoMaterials == null) return FallBackToIdle("cargoMaterialsが欠損しています");
            if (saveData.CargoMaterials.Any(item => item == null)) return FallBackToIdle("cargoMaterialsのnull要素が欠損しています");
            var phase = (RocketLaunchPadPhase)saveData.Phase.Value;
            var holdsCargo = phase is RocketLaunchPadPhase.Building or RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.ReadyToLaunch;

            // 生DTOを検証してから生成し、捨てる積荷は実体化しない
            // Validate raw DTOs before creation and never instantiate discarded cargo
            if (!holdsCargo)
            {
                if (0 < saveData.CargoPayload.Count || saveData.CargoMaterials.Any(item => 0 < item.Count))
                    Debug.LogWarning($"発射台のセーブで積荷を持たない段階に積荷があったため捨てます 段階:{phase}");
                return phase switch
                {
                    RocketLaunchPadPhase.Idle => IdleStage.Instance,
                    RocketLaunchPadPhase.AwaitingBuildCommand => AwaitingBuildCommandStage.Instance,
                    _ => new LaunchingStage(RestoreRemainingTicks()),
                };
            }
            var payloadId = Guid.TryParse(saveData.CargoPayload.ItemGuidStr, out var payloadGuid) ? MasterHolder.ItemMaster.GetItemIdOrNull(payloadGuid) : null;
            if (!payloadId.HasValue || !layout.TryGetPayloadRequirement(payloadId.Value, out var requirement) || saveData.CargoPayload.Count != requirement.Count ||
                !CanCreate(saveData.CargoPayload) || !saveData.CargoMaterials.All(CanCreate))
                return FallBackToIdle($"積荷が今の1便ぶんと合わず打ち上げられないため積荷を捨てます 段階:{phase}");
            var cargo = new RocketCargo(saveData.CargoPayload.ToItemStack(), saveData.CargoMaterials.Select(item => item.ToItemStack()).ToList());
            return phase switch
            {
                RocketLaunchPadPhase.Building => new BuildingStage(cargo, RestoreRemainingTicks()),
                RocketLaunchPadPhase.AwaitingLaunchCommand => new AwaitingLaunchCommandStage(cargo),
                _ => new ReadyToLaunchStage(cargo),
            };

            #region Internal

            uint RestoreRemainingTicks()
            {
                // 進行中の段階は残り1tick以上。壊れた0以下の値は警告して1tickにする
                // Progressing stages need one tick; warn and repair non-positive remaining time
                if (!saveData.RemainingSeconds.HasValue)
                {
                    Debug.LogWarning($"発射台のセーブの残り時間が欠落しているため1tickにします 段階:{phase}");
                    return 1;
                }
                var ticks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds.Value);
                if (0 < ticks) return ticks;
                Debug.LogWarning($"発射台のセーブの残り時間が0以下のため1tickにします 段階:{phase} 残り秒:{saveData.RemainingSeconds}");
                return 1;
            }

            RocketLaunchPadStage FallBackToIdle(string reason)
            {
                Debug.LogWarning($"発射台のセーブを待機として読み込みます {reason}");
                return IdleStage.Instance;
            }

            #endregion
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
