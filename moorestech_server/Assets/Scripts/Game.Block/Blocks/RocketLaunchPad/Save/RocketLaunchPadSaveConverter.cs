using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
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
    public static class RocketLaunchPadSaveConverter
    {
        public static RocketLaunchPadSaveJsonObject Capture(RocketLaunchPadStage stage, int nextPayloadIndex, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            var cargo = stage is LoadedRocketStage loaded ? loaded.Cargo : new RocketCargo(ServerContext.ItemStackFactory.CreatEmpty(), new List<IItemStack>());
            var items = slots.InventoryItems;
            return new RocketLaunchPadSaveJsonObject
            {
                Phase = (int)stage.Phase,
                RemainingSeconds = GameUpdater.TicksToSeconds(RemainingTicksOf(stage)),
                CargoPayload = new ItemStackSaveJsonObject(cargo.Payload),
                CargoMaterials = cargo.Materials.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                NextPayloadIndex = nextPayloadIndex,
                InputItems = items.Take(layout.OutputSlotStart).Where(item => item.Count > 0).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputItems = layout.OutputSlots.Select(slot => new ItemStackSaveJsonObject(items[slot])).ToList(),
            };
        }

        public static void RestoreSlots(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            // スロットをイベント無しで戻す。マスタ変更で置き場が無くなった品は警告して捨てる（打ち上げ後の保留は持たない）
            // Restore slots without events; items that lost their place through a master change are dropped with a warning (no pending store)
            var droppedCount = 0;
            foreach (var input in saveData.InputItems.Select(item => item.ToItemStack()).Where(item => item.Count > 0))
            {
                if (layout.TryGetInputSlot(input.Id, out var slot)) slots.SetItemWithoutEvent(slot, input);
                else droppedCount += input.Count;
            }
            var outputs = saveData.OutputItems.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputs.Count; i++)
            {
                if (i < layout.OutputSlotCount) slots.SetItemWithoutEvent(layout.OutputSlots[i], outputs[i]);
                else droppedCount += outputs[i].Count;
            }
            if (droppedCount > 0) Debug.LogWarning($"発射台のスロット構成がセーブ時と違うため、置き場の無い品を {droppedCount} 個捨てました");
        }

        public static RocketLaunchPadStage RestoreStage(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout)
        {
            var phase = (RocketLaunchPadPhase)saveData.Phase;
            var cargo = new RocketCargo(saveData.CargoPayload.ToItemStack(), saveData.CargoMaterials.Select(item => item.ToItemStack()).ToList());
            var isLaunchableCargo = IsLaunchableCargo(cargo, layout);

            // 積載中の型は、積荷が今の並びの1便ぶんのときだけ作る（欠損マスタの刈り込みで空になった積荷もここで弾く）
            // Loaded stage types are built only when the cargo is one launch worth under the current layout (pruned empty cargo is rejected too)
            RocketLaunchPadStage stage = phase switch
            {
                RocketLaunchPadPhase.Idle => IdleStage.Instance,
                RocketLaunchPadPhase.AwaitingBuildCommand => AwaitingBuildCommandStage.Instance,
                RocketLaunchPadPhase.Building when isLaunchableCargo => new BuildingStage(cargo, RestoreRemainingTicks(saveData, phase)),
                RocketLaunchPadPhase.AwaitingLaunchCommand when isLaunchableCargo => new AwaitingLaunchCommandStage(cargo),
                RocketLaunchPadPhase.ReadyToLaunch when isLaunchableCargo => new ReadyToLaunchStage(cargo),
                RocketLaunchPadPhase.Launching => new LaunchingStage(RestoreRemainingTicks(saveData, phase)),
                _ => null,
            };

            // どの型にも当たらないのは、積載中の段階で積荷が合わないときか、不明な段階の値のとき
            // No type matches only for a loaded phase with mismatched cargo or an unknown phase value
            if (stage == null && Enum.IsDefined(typeof(RocketLaunchPadPhase), phase)) return FallBackToIdle($"積荷が今の1便ぶんと合わず打ち上げられないため積荷を捨てます 段階:{phase} 積荷:{cargo.Payload.Id}×{cargo.Payload.Count}");
            if (stage == null) return FallBackToIdle($"段階の値が不明です 段階:{saveData.Phase}");

            // 積荷を持つかは段階の型の階層が決める。持たない型に積荷が残っていたら捨てる
            // The stage type hierarchy decides whether cargo is held; cargo left on a type that holds none is dropped
            if (stage is not LoadedRocketStage && cargo.IsLoaded) Debug.LogWarning($"発射台のセーブで積荷を持たない段階に積荷があったため捨てます 段階:{phase}");
            return stage;
        }

        // 積んだロケットは常に1便ぶん（入れ口の要求と同じ品・同じ個数）。積荷表の受け付けは軌道到着サービスの Arrive が持ち、ここでは写さない
        // A loaded rocket always holds one launch worth (same item and count as the input requirement); table acceptance stays with Arrive
        private static bool IsLaunchableCargo(RocketCargo cargo, RocketLaunchPadSlotLayout layout)
        {
            return layout.TryGetPayloadRequirement(cargo.Payload.Id, out var requirement) && cargo.Payload.Count == requirement.Count;
        }

        private static uint RestoreRemainingTicks(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadPhase phase)
        {
            // 進行中の段階は残り1tick以上。壊れた0以下の値は警告して1tickにする
            // Progressing stages have at least one tick left; a broken non-positive value is set to one tick with a warning
            var ticks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds);
            if (ticks > 0) return ticks;
            Debug.LogWarning($"発射台のセーブの残り時間が0以下のため1tickにします 段階:{phase} 残り秒:{saveData.RemainingSeconds}");
            return 1;
        }

        private static uint RemainingTicksOf(RocketLaunchPadStage stage)
        {
            return stage is IProgressingStage progressing ? progressing.RemainingTicks : 0;
        }

        private static RocketLaunchPadStage FallBackToIdle(string reason)
        {
            Debug.LogWarning($"発射台のセーブを待機として読み込みます {reason}");
            return IdleStage.Instance;
        }
    }
}
