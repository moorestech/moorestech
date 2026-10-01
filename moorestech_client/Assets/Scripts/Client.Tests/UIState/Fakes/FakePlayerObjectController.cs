using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Tests.UIState.Fakes
{
    // 移動ロックの適用だけを記録する自機ダブル
    // A player double that records nothing but the applied movement locks
    public class FakePlayerObjectController : IPlayerObjectController
    {
        public bool? LastUiLock { get; private set; }
        public int UiLockCallCount { get; private set; }
        public Vector3 Position => Vector3.zero;

        public void SetMovementLock(PlayerMovementLockReason reason, bool isLocked)
        {
            // Ui以外の理由は対象外。取り違えを検知するため理由ごとに記録を分ける
            // Reasons other than Ui are out of scope; record per reason so a mix-up is caught
            if (reason != PlayerMovementLockReason.Ui) return;
            LastUiLock = isLocked;
            UiLockCallCount++;
        }

        public void SetPlayerPosition(Vector3 playerPos)
        {
        }

        public void SetActive(bool active)
        {
        }

        public void SetAnimationState(string state)
        {
        }

        public void SetModelVisible(bool visible)
        {
        }
    }
}
