using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private BlockInventoryBase _blockInventory;
        
        private Vector3Int _openBlockPos;
        
        public BlockInventoryState(BlockGameObjectDataStore blockGameObjectDataStore, PlayerInventoryViewController playerInventoryViewController)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerInventoryViewController = playerInventoryViewController;
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag, OnOpenableBlockInventoryUpdateEvent);
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            BlockGameObject blockGameObject = null;
            if (!IsBlockOpenable())
            {
                return;
            }
            
            RequestToServer();
            
            SetUIObject();
            
            #region Internal
            
            bool IsBlockOpenable()
            {
                if (!BlockClickDetect.TryGetCursorOnBlockPosition(out _openBlockPos))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");
                    return false;
                }
                
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(_openBlockPos, out blockGameObject))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError("開いたブロックの情報が取得できませんでした。");
                    return false;
                }
                
                //ブロックインベントリのビューを設定する
                var blockMaster = blockGameObject.BlockMasterElement;
                var path = blockMaster.BlockUIAddressablesPath;
                if (AddressableLoader.TryLoad<BlockInventoryBase>(path, out var blockInventoryPrefab))
                {
                    _blockInventory = ClientContext.DIContainer.Instantiate(blockInventoryPrefab, _playerInventoryViewController.transform);
                    return true;
                }
                
                // TODO ログ基盤に入れる
                Debug.LogError($"ブロックインベントリのビューが取得できませんでした。 Guid:{blockMaster.BlockGuid} Name:{blockMaster.Name} Path:{path}");
                return false;
            }
            
            void RequestToServer()
            {
                // 開いているブロックの設定
                // Open block settings
                ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, true);
                // ブロックのインベントリを取得し、適用する
                // Get the block inventory and apply it
                UpdateBlockInventory(_openBlockPos, default).Forget();
            }
            
            async UniTask UpdateBlockInventory(Vector3Int pos, CancellationToken ct)
            {
                var response = await ClientContext.VanillaApi.Response.GetBlockInventory(pos, ct);
                _blockInventory?.SetItemList(response);
            }
            
            void SetUIObject()
            {
                // カーソルを表示する
                // Show cursor
                InputManager.MouseCursorVisible(true);
                
                // UIのオブジェクトをオンにする
                // Turn on the UI object
                _blockInventory.OpenBlockInventoryType(blockGameObject);
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_blockInventory);
            }
            
            #endregion
        }
        
        public void OnExit()
        {
            // ブロックを閉じる設定
            // Close block settings
            ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, false);
            
            // ブロックインベントリを閉じる
            // Close the block inventory
            _blockInventory.CloseBlockInventory();
            _playerInventoryViewController.SetActive(false);
            GameObject.Destroy(_blockInventory.gameObject);
            _blockInventory = null;
        }
        
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            if (_blockInventory == null) return;
            
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _blockInventory.UpdateInventorySlot(packet.Slot, item);
        }
    }
}