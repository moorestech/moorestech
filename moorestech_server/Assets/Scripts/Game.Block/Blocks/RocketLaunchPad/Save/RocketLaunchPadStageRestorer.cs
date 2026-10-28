using System;
using System.Linq;
using Core.Inventory;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    internal static class RocketLaunchPadStageRestorer
    {
        public static RocketLaunchPadStage RestoreStage(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            if (!saveData.Phase.HasValue || !Enum.IsDefined(typeof(RocketLaunchPadPhase), saveData.Phase.Value))
                return FallBackToIdle($"段階の値が不明です 段階:{saveData.Phase}");
            if (saveData.CargoPayload == null) return FallBackToIdle("cargoPayloadが欠損しています");
            if (saveData.CargoMaterials == null) return FallBackToIdle("cargoMaterialsが欠損しています");
            if (saveData.CargoMaterials.Any(item => item == null)) return FallBackToIdle("cargoMaterialsのnull要素が欠損しています");
            var phase = (RocketLaunchPadPhase)saveData.Phase.Value;
            var holdsCargo = RocketLaunchPadSaveConverter.HoldsCargo(phase);

            // 段階に合わない積荷も回収欄へ戻して保持する
            // Preserve cargo inconsistent with its phase by returning it to take-out slots
            if (!holdsCargo)
            {
                if (0 < saveData.CargoPayload.Count || saveData.CargoMaterials.Any(item => 0 < item.Count))
                {
                    Debug.LogWarning($"発射台のセーブで積荷を持たない段階に積荷があったためスロットへ戻します 段階:{phase}");
                    RocketLaunchPadCargoRecovery.ReturnCargo(saveData, layout, slots);
                }
                return phase switch
                {
                    RocketLaunchPadPhase.Idle => IdleStage.Instance,
                    RocketLaunchPadPhase.AwaitingBuildCommand => AwaitingBuildCommandStage.Instance,
                    RocketLaunchPadPhase.Launching => new LaunchingStage(RestoreRemainingTicks()),
                    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "積荷を持たない段階として未対応の値です"),
                };
            }
            if (!RocketLaunchPadRetainedItemValidator.IsRetainableCargo(saveData, layout))
                return FallBackToIdle($"積荷が今の1便ぶんと合わず打ち上げられないためスロットへ戻します 段階:{phase}");
            var cargo = new RocketCargo(saveData.CargoPayload.ToItemStack(), saveData.CargoMaterials.Select(item => item.ToItemStack()).ToList());
            return phase switch
            {
                RocketLaunchPadPhase.Building => new BuildingStage(cargo, RestoreRemainingTicks()),
                RocketLaunchPadPhase.AwaitingLaunchCommand => new AwaitingLaunchCommandStage(cargo),
                RocketLaunchPadPhase.ReadyToLaunch => new ReadyToLaunchStage(cargo),
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "積荷を持つ段階として未対応の値です"),
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
                RocketLaunchPadCargoRecovery.ReturnCargo(saveData, layout, slots);
                return IdleStage.Instance;
            }

            #endregion
        }

    }
}
