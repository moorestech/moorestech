using System;
using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly BlockInventoryObjectCreator _blockInventory;
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;
        private readonly CraftRecipeItemListViewer _craftRecipeItemListViewer;

        private readonly ItemRecipePresenter _itemRecipePresenter;
        private readonly SinglePlayInterface _singlePlayInterface;

        public BlockInventoryState(BlockInventoryObjectCreator blockInventory,
            CraftRecipeItemListViewer craftRecipeItemListViewer, ItemRecipePresenter itemRecipePresenter, IBlockClickDetect blockClickDetect, ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, SinglePlayInterface singlePlayInterface)
        {
            _craftRecipeItemListViewer = craftRecipeItemListViewer;
            _itemRecipePresenter = itemRecipePresenter;
            _blockClickDetect = blockClickDetect;
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _singlePlayInterface = singlePlayInterface;
            _blockInventory = blockInventory;
            blockInventory.gameObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            if (_itemRecipePresenter.IsClicked) return UIStateEnum.RecipeViewer;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!_blockClickDetect.TryGetCursorOnBlockPosition(out var blockPos)) Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");

            OnOpenBlockInventory?.Invoke(blockPos);

            //UIのオブジェクトをオンにする
            _craftRecipeItemListViewer.gameObject.SetActive(true);
            _blockInventory.gameObject.SetActive(true);


            //ブロックインベントリのビューを設定する
            if (!_chunkBlockGameObjectDataStore.ContainsBlockGameObject(blockPos)) return;

            var id = _chunkBlockGameObjectDataStore.GetBlockGameObject(blockPos).BlockId;
            var config = _singlePlayInterface.BlockConfig.GetBlockConfig(id);

            switch (config.Type)
            {
                case VanillaBlockType.Chest:
                {
                    var configParam = config.Param as ChestConfigParam;
                    _blockInventory.SetOneSlotInventory(config.Name, configParam.ChestItemNum, blockPos);
                    break;
                }
                case VanillaBlockType.Miner:
                {
                    var configParam = config.Param as MinerBlockConfigParam;
                    _blockInventory.SetMinerInventory(config.Name, configParam.OutputSlot, blockPos);
                    break;
                }
                case VanillaBlockType.Generator:
                {
                    var configParam = config.Param as PowerGeneratorConfigParam;
                    _blockInventory.SetOneSlotInventory(config.Name, configParam.FuelSlot, blockPos);
                    break;
                }
                case VanillaBlockType.Machine:
                {
                    var configParam = config.Param as MachineBlockConfigParam;
                    _blockInventory.SetIOSlotInventory(config.Name, configParam.InputSlot, configParam.OutputSlot, blockPos);
                    break;
                }
            }
        }

        public void OnExit()
        {
            OnCloseBlockInventory?.Invoke();

            _blockInventory.gameObject.SetActive(false);
            _craftRecipeItemListViewer.gameObject.SetActive(false);
        }

        public event Action<Vector2Int> OnOpenBlockInventory;
        public event Action OnCloseBlockInventory;
    }
}