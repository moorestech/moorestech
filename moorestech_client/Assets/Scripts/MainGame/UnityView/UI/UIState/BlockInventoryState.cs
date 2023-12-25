using System;
using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using MainGame.Network.Send;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MainGame.UnityView.UI.UIState.UIObject;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;

        private readonly SinglePlayInterface _singlePlayInterface;
        private readonly BlockInventoryView _blockInventoryView;
        private readonly PlayerInventoryController _playerInventoryController;
        
        private readonly SendBlockInventoryOpenCloseControlProtocol _blockInventoryOpenCloseControlProtocol;
        private readonly SendRequestBlockInventoryProtocol _sendRequestBlockInventoryProtocol;
        
        private Vector2Int _openBlockPos;

        public BlockInventoryState(BlockInventoryView blockInventoryView, IBlockClickDetect blockClickDetect, ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, SinglePlayInterface singlePlayInterface,PlayerInventoryController playerInventoryController,SendRequestBlockInventoryProtocol sendRequestBlockInventoryProtocol, SendBlockInventoryOpenCloseControlProtocol blockInventoryOpenCloseControlProtocol)
        {
            _blockClickDetect = blockClickDetect;
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _singlePlayInterface = singlePlayInterface;
            _playerInventoryController = playerInventoryController;
            _blockInventoryView = blockInventoryView;
            
            _sendRequestBlockInventoryProtocol = sendRequestBlockInventoryProtocol;
            _blockInventoryOpenCloseControlProtocol = blockInventoryOpenCloseControlProtocol;
            
            blockInventoryView.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!_blockClickDetect.TryGetCursorOnBlockPosition(out _openBlockPos)) Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");

            //サーバーのリクエスト
            _sendRequestBlockInventoryProtocol.Send(_openBlockPos);
            _blockInventoryOpenCloseControlProtocol.Send(_openBlockPos, true);

            //UIのオブジェクトをオンにする
            _blockInventoryView.SetActive(true);
            _playerInventoryController.SetActive(true);
            _playerInventoryController.SetSubInventory(_blockInventoryView);

            //ブロックインベントリのビューを設定する
            if (!_chunkBlockGameObjectDataStore.ContainsBlockGameObject(_openBlockPos)) return;

            var id = _chunkBlockGameObjectDataStore.GetBlockGameObject(_openBlockPos).BlockId;
            var config = _singlePlayInterface.BlockConfig.GetBlockConfig(id);
            
            var type = config.Type switch
            {
                VanillaBlockType.Chest => BlockInventoryType.Chest,
                VanillaBlockType.Miner => BlockInventoryType.Miner,
                VanillaBlockType.Machine => BlockInventoryType.Machine,
                VanillaBlockType.Generator => BlockInventoryType.Generator,
                _ => throw new ArgumentOutOfRangeException()
            };

            _blockInventoryView.SetBlockInventoryType(type,_openBlockPos,config.Param);
        }

        public void OnExit()
        {
            _blockInventoryOpenCloseControlProtocol.Send(_openBlockPos, false);

            _blockInventoryView.SetActive(false);
            _playerInventoryController.SetActive(false);
        }
    }
}