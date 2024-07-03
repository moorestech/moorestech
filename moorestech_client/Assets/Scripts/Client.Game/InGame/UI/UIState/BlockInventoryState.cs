using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.Sub;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Block;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly BlockInventoryView _blockInventoryView;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private CancellationTokenSource _cancellationTokenSource;
        
        private Vector3Int _openBlockPos;
        
        public BlockInventoryState(BlockInventoryView blockInventoryView, BlockGameObjectDataStore blockGameObjectDataStore, PlayerInventoryViewController playerInventoryViewController)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerInventoryViewController = playerInventoryViewController;
            _blockInventoryView = blockInventoryView;
            
            blockInventoryView.SetActive(false);
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!BlockClickDetect.TryGetCursorOnBlockPosition(out _openBlockPos)) Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");
            if (!_blockGameObjectDataStore.ContainsBlockGameObject(_openBlockPos)) return;
            
            InputManager.MouseCursorVisible(true);
            
            //サーバーのリクエスト
            ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, true);
            _cancellationTokenSource = new CancellationTokenSource();
            UpdateBlockInventory(_openBlockPos, _cancellationTokenSource.Token).Forget();
            
            
            //ブロックインベントリのビューを設定する
            var id = _blockGameObjectDataStore.GetBlockGameObject(_openBlockPos).BlockId;
            var config = ServerContext.BlockConfig.GetBlockConfig(id);
            
            var type = config.Type switch
            {
                VanillaBlockType.Chest => BlockInventoryType.Chest,
                VanillaBlockType.ElectricMiner => BlockInventoryType.Miner,
                VanillaBlockType.ElectricMachine => BlockInventoryType.Machine,
                VanillaBlockType.ElectricGenerator => BlockInventoryType.Generator,
                _ => throw new ArgumentOutOfRangeException(),
            };
            
            _blockInventoryView.SetBlockInventoryType(type, _openBlockPos, config.Param, id);
            
            //UIのオブジェクトをオンにする
            _blockInventoryView.SetActive(true);
            _playerInventoryViewController.SetActive(true);
            _playerInventoryViewController.SetSubInventory(_blockInventoryView);
        }
        
        public void OnExit()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            
            ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, false);
            
            _blockInventoryView.SetActive(false);
            _playerInventoryViewController.SetActive(false);
        }
        
        private async UniTask UpdateBlockInventory(Vector3Int pos, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.GetBlockInventory(pos, ct);
            _blockInventoryView.SetItemList(response);
        }
    }
}