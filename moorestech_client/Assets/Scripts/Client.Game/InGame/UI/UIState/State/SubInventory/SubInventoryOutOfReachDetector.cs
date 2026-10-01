using Client.Game.InGame.Interact.Selection;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    /// <summary>
    /// 対象が届かなくなったか判定し、理由をログ出力
    /// Decides whether the opened target has left reach and logs why
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
                LogAutoClose("reach target view missing");
                return true;
            }

            // 無言で閉じると原因を辿れないため、距離・対話不能・破棄を書き分けて残す
            // Closing silently leaves no trail, so out of range, no longer interactable and destroyed are logged apart
            switch (_reachQuery.QueryReach(reachTarget, playerPosition))
            {
                case InteractReachResult.Reachable:
                    return false;
                case InteractReachResult.OutOfRange:
                    LogAutoClose("out of range");
                    return true;
                case InteractReachResult.NotInteractable:
                    LogAutoClose("target no longer interactable");
                    return true;
                case InteractReachResult.TargetDestroyed:
                    LogAutoClose("target view destroyed");
                    return true;
                default:
                    LogAutoClose("unknown reach result");
                    return true;
            }

            #region Internal

            void LogAutoClose(string cause)
            {
                var identifier = source.InventoryIdentifier;
                Debug.Log($"SubInventory auto-closed: {cause}. source={source.GetType().Name}, inventory={identifier.InventoryType}, blockPosition={identifier.BlockPosition}, trainCarInstanceId={identifier.TrainCarInstanceId}, playerPosition={playerPosition}");
            }

            #endregion
        }
    }
}
