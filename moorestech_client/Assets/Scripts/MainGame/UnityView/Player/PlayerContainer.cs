using UnityEngine;

namespace MainGame.UnityView.Player
{
    public class PlayerContainer : MonoBehaviour
    {
        public PlayerGrabItemManager PlayerGrabItemManager => playerGrabItemManager;
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        
        public IPlayerObjectController PlayerObjectController => playerObjectController;
        [SerializeField] private PlayerObjectController playerObjectController;
    }
}