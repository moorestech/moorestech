using System;
using Client.Input;
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
            playerObjectController.Initialize(initialHandshakeResponse.PlayerPos, initialHandshakeResponse.MapLayout.Spawn);

            // 文字入力中の停止は自機の初期化後に張る。フォーカスは受信スレッド発なのでメインスレッドへ移す
            // Hook the text-input stop after the player is initialized; focus comes from the socket thread, so hop to the main thread
            new TextInputMovementLockApplier(playerObjectController).Initialize(WebUiInputExclusivity.TextInputFocused.ObserveOnMainThread());
        }

        // 地形構築の完了をFinalizerから受けて、自機の実行を開始する
        // Receives terrain-build completion from the finalizer and starts the player runtime
        public void StartPlayerRuntime()
        {
            playerObjectController.StartPlayerRuntime();
        }
    }
}