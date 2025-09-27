using System;
using Client.Game.Skit.Starter;
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
        public PlayerSkitStarterDetector PlayerSkitStarterDetector => playerSkitStarterDetector;
        
        
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        [SerializeField] private PlayerObjectController playerObjectController;
        [SerializeField] private PlayerSkitStarterDetector playerSkitStarterDetector;
        
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