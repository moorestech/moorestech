using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // - 昇降中の衛星と残り時間を復元
    // - 現マスタで有効な形へ正規化
    // - Restore the climbing satellite and remaining time
    // - Normalize them against the current master
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

            // 今のマスタで1便ぶんとして昇れる分だけを残す。昇らない分は在庫側が戻す
            // Keep only what can climb as one launch under the current master; the inventory returns the rest
            satellite = inventory.TakeLaunchableClimbingPayloadOnLoad(satellite);
            // silent-ok: 昇降の取消は在庫側がLogErrorで理由を出している / 開発者がロード時のログで気づく
            if (satellite.Id == ItemMaster.EmptyItemId) return (satellite, 0);

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
