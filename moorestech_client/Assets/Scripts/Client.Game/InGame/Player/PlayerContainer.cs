using UnityEngine;

namespace Client.Game.InGame.Player
{
    public class PlayerContainer : MonoBehaviour
    {
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        [SerializeField] private PlayerObjectController playerObjectController;
        public PlayerGrabItemManager PlayerGrabItemManager => playerGrabItemManager;

        public IPlayerObjectController PlayerObjectController => playerObjectController;
    }
}