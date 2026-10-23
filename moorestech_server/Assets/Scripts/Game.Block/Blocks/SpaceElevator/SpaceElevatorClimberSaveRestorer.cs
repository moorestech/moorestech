using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // セーブの昇降中の衛星と残りを、今のマスタで成り立つ形に直して返す
    // Normalises the saved climbing satellite and remaining time to what the current master allows
    internal static class SpaceElevatorClimberSaveRestorer
    {
        internal static SpaceElevatorClimberSaveJsonObject ToSave(bool isConnected, IItemStack satellite, uint remainingTicks)
        {
            // マスタ値ではなく進行中の秒数だけを保存する
            // Save elapsed state as seconds rather than master-dependent tick totals
            return new SpaceElevatorClimberSaveJsonObject
            {
                IsConnected = isConnected,
                ClimbingSatellite = new ItemStackSaveJsonObject(satellite),
                RemainingClimbSeconds = GameUpdater.TicksToSeconds(remainingTicks),
            };
        }

        internal static (IItemStack Satellite, uint RemainingTicks) Restore(SpaceElevatorClimberSaveJsonObject save, SpaceElevatorBaseInventoryComponent inventory, uint climbTotalTicks)
        {
            var satellite = save.ClimbingSatellite.ToItemStack();
            if (satellite.Id == ItemMaster.EmptyItemId)
            {
                // 衛星が無いのに残りがあるのは壊れたセーブ。昇降していない状態で戻す
                // A remaining time without a satellite means a broken save; restore as not climbing
                if (save.RemainingClimbSeconds != 0) Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星が無いのに残り {save.RemainingClimbSeconds} 秒がセーブにあるため、昇降していない状態で戻します");
                return (satellite, 0);
            }

            // 運ぶ積荷から外れた衛星は頂上で軌道到着サービスが例外を出すので、昇降を取り消して入口へ戻す
            // A satellite no longer carried would make the orbital service throw at the top, so cancel the climb and return it to the inlet
            if (!inventory.IsCarriedPayload(satellite.Id))
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星 {satellite.Id} は今のマスタで運ぶ積荷ではないため、昇降を取り消して入口へ戻します");
                inventory.ReturnSatelliteWithoutEvent(satellite);
                return (ServerContext.ItemStackFactory.CreatEmpty(), 0);
            }

            // 昇降機は1機ずつ運ぶ。壊れたセーブで2機以上あれば1機だけ昇らせ、余りを入口へ戻す
            // The climber carries one at a time; a broken save with more keeps one climbing and returns the rest to the inlet
            if (1 < satellite.Count)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星が {satellite.Count} 機セーブにあるため、1機だけ昇らせ残りを入口へ戻します");
                inventory.ReturnSatelliteWithoutEvent(ServerContext.ItemStackFactory.Create(satellite.Id, satellite.Count - 1));
                satellite = ServerContext.ItemStackFactory.Create(satellite.Id, 1);
            }

            // 残りが負・NaN・無限のセーブは壊れている。最初から昇り直す（0は頂上で出口待ちの正しい値）
            // A negative, NaN or infinite remaining time is a broken save; climb again from the start (zero is the valid waiting-at-top value)
            if (!(0 <= save.RemainingClimbSeconds) || double.IsInfinity(save.RemainingClimbSeconds))
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降の残り {save.RemainingClimbSeconds} 秒は不正なため、最初から昇り直します");
                return (satellite, climbTotalTicks);
            }

            // マスタで昇降時間が縮んだときは総時間に収め、進みが負にならないようにする
            // When the master shortened the climb, clamp to the total so the progress never goes negative
            var remainingTicks = GameUpdater.SecondsToTicks(save.RemainingClimbSeconds);
            if (remainingTicks <= climbTotalTicks) return (satellite, remainingTicks);
            Debug.LogWarning($"[SpaceElevatorBase] 昇降の残り {remainingTicks}tick が総時間 {climbTotalTicks}tick を超えるため、総時間に収めます");
            return (satellite, climbTotalTicks);
        }
    }
}
