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
            // スロットをイベント無しで戻す。マスタ変更で置き場が無くなった品は警告して捨てる（打ち上げ後の保留は持たない）
            // Restore slots without events; items that lost their place through a master change are dropped with a warning (no pending store)
            long droppedCount = 0;
            foreach (var input in saveData.InputItems)
            {
                var id = Guid.TryParse(input.ItemGuidStr, out var inputGuid) ? MasterHolder.ItemMaster.GetItemIdOrNull(inputGuid) : null;
                if (id.HasValue && layout.TryGetInputSlot(id.Value, out var slot) && CanCreate(input))
                    slots.SetItemWithoutEvent(slot, input.ToItemStack());
                else droppedCount += Math.Max(1, input.Count);
            }
            for (var i = 0; i < saveData.OutputItems.Count; i++)
            {
                var output = saveData.OutputItems[i];
                if (i < layout.OutputSlotCount && CanCreate(output)) slots.SetItemWithoutEvent(layout.OutputSlots[i], output.ToItemStack());
                else droppedCount += Math.Max(1, output.Count);
            }
            if (0 < droppedCount) Debug.LogWarning($"発射台のスロット構成または保存値が無効なため、復元できない品を {droppedCount} 個捨てました");
        }

        public static RocketLaunchPadStage RestoreStage(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout)
        {
            if (!saveData.Phase.HasValue || !Enum.IsDefined(typeof(RocketLaunchPadPhase), saveData.Phase.Value))
                return FallBackToIdle($"段階の値が不明です 段階:{saveData.Phase}");
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
                var ticks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds);
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
        private static bool CanCreate(ItemStackSaveJsonObject item)
        {
            if (item.Count == 0) return true;
            var id = Guid.TryParse(item.ItemGuidStr, out var guid) ? MasterHolder.ItemMaster.GetItemIdOrNull(guid) : null;
            return 0 < item.Count && id.HasValue && item.Count <= ServerContext.GetService<IItemStackLevelLookup>().GetMaxStack(id.Value);
        }
    }
}
