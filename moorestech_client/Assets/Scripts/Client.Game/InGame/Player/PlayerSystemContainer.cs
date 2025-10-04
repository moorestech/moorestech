using System;
using Client.Network.API;
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
        }
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            playerObjectController.Initialize(initialHandshakeResponse);
        }
    }
}