using System;
using Client.Network.API;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Player
{
    public class PlayerSystemContainer : MonoBehaviour
    {
        public static PlayerSystemContainer Instance { get; private set; }
        
        public PlayerGrabItemManager PlayerGrabItemManager => playerGrabItemManager;
        public IPlayerObjectController PlayerObjectController => playerObjectController;
        
        
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        [SerializeField] private PlayerObjectController playerObjectController;
        
        private void Awake()
        {
            Instance = this;

            // 手持ちアイテムが差し替わると新Rendererが表示状態で生えるため、自機の表示状態を再適用する
            // A swapped grab item spawns visible renderers, so re-apply the player model visibility
            playerGrabItemManager.OnGrabItemChanged.Subscribe(_ => playerObjectController.RefreshModelVisible()).AddTo(this);
        }
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            playerObjectController.Initialize(initialHandshakeResponse);
        }
    }
}