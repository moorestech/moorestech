using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 削除対象が破棄済みかを判定し、破棄済みなら理由をログに残す
    // Checks whether a delete target was destroyed and logs why it is skipped
    public static class DeleteTargetLiveness
    {
        internal static bool IsAliveOrWarn(IDeleteTarget target, string destroyedWarning)
        {
            if (target is Object unityTarget && unityTarget == null)
            {
                Debug.LogWarning(destroyedWarning);
                return false;
            }
            return true;
        }
    }
}
