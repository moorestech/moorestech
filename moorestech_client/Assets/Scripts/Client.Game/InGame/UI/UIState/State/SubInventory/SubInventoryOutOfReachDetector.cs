using Client.Game.InGame.Interact.Selection;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    /// <summary>
    /// 開いたインベントリの対象へ手が届かなくなったかを判定し、閉じる理由をログに残す
    /// Decides whether the opened inventory's target has left reach and logs why it closes
    /// </summary>
    public class SubInventoryOutOfReachDetector
    {
        private readonly InteractReachQuery _reachQuery = new();

        public bool IsOutOfReach(ISubInventorySource source, Vector3 playerPosition)
        {
            // 表示が消えて対象を引けなければ、届く対象が無いので閉じる
            // When the view is gone and the target cannot be resolved, nothing is reachable, so close
            if (!source.TryGetReachTarget(out var reachTarget))
            {
                LogAutoClose(source, playerPosition, "reach target view missing");
                return true;
            }

            if (_reachQuery.IsWithinReach(reachTarget, playerPosition)) return false;

            // 無言で閉じると原因を辿れないため、対象のIDと範囲外/対話不能の別を残す
            // Closing silently leaves no trail, so log the target ID and whether it is out of range or no longer interactable
            LogAutoClose(source, playerPosition, $"out of range or not interactable (interactAvailable={reachTarget.IsInteractAvailable})");
            return true;
        }

        private static void LogAutoClose(ISubInventorySource source, Vector3 playerPosition, string cause)
        {
            var identifier = source.InventoryIdentifier;
            Debug.Log($"SubInventory auto-closed: {cause}. source={source.GetType().Name}, inventory={identifier.InventoryType}, blockPosition={identifier.BlockPosition}, trainCarInstanceId={identifier.TrainCarInstanceId}, playerPosition={playerPosition}");
        }
    }
}
