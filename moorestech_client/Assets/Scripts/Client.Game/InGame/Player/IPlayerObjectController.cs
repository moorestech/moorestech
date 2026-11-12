using UnityEngine;

namespace Client.Game.InGame.Player
{
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public void SetPlayerPosition(Vector3 playerPos);
        public void SetActive(bool active);
        
        public void SetAnimationState(string state);
        public void SetMovementLock(PlayerMovementLockReason reason, bool isLocked);
        public void SetModelVisible(bool visible);
        public void SetSprintToggleAllowed(bool allowed);
    }
}
