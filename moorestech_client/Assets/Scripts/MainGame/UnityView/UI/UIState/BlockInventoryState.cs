using System;
using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
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
        private readonly BlockInventoryObject _blockInventoryObject;
        private readonly PlayerInventoryController _playerInventoryController;

        public BlockInventoryState(BlockInventoryObject blockInventoryObject, IBlockClickDetect blockClickDetect, ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, SinglePlayInterface singlePlayInterface,PlayerInventoryController playerInventoryController)
        {
            _blockClickDetect = blockClickDetect;
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _singlePlayInterface = singlePlayInterface;
            _playerInventoryController = playerInventoryController;
            _blockInventoryObject = blockInventoryObject;
            blockInventoryObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!_blockClickDetect.TryGetCursorOnBlockPosition(out var blockPos)) Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");

            OnOpenBlockInventory?.Invoke(blockPos);

            //UIのオブジェクトをオンにする
            _blockInventoryObject.SetActive(true);
            _playerInventoryController.SetActive(true);


            //ブロックインベントリのビューを設定する
            if (!_chunkBlockGameObjectDataStore.ContainsBlockGameObject(blockPos)) return;

            var id = _chunkBlockGameObjectDataStore.GetBlockGameObject(blockPos).BlockId;
            var config = _singlePlayInterface.BlockConfig.GetBlockConfig(id);

            switch (config.Type)
            {
                case VanillaBlockType.Chest:
                {
                    var configParam = config.Param as ChestConfigParam;
                    _blockInventoryObject.SetBlockInventoryType(BlockInventoryType.Chest);
                    break;
                }
                case VanillaBlockType.Miner:
                {
                    var configParam = config.Param as MinerBlockConfigParam;
                    _blockInventoryObject.SetBlockInventoryType(BlockInventoryType.Miner);
                    break;
                }
                case VanillaBlockType.Generator:
                {
                    var configParam = config.Param as PowerGeneratorConfigParam;
                    _blockInventoryObject.SetBlockInventoryType(BlockInventoryType.Generator);
                    break;
                }
                case VanillaBlockType.Machine:
                {
                    var configParam = config.Param as MachineBlockConfigParam;
                    _blockInventoryObject.SetBlockInventoryType(BlockInventoryType.Machine);
                    break;
                }
            }
        }

        public void OnExit()
        {
            OnCloseBlockInventory?.Invoke();

            _blockInventoryObject.SetActive(false);
            _playerInventoryController.SetActive(false);
        }

        public event Action<Vector2Int> OnOpenBlockInventory;
        public event Action OnCloseBlockInventory;
    }
}