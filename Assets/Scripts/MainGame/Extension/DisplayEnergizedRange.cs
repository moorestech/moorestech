using System;
using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.Service;
using Core.Const;
using Game.PlayerInventory.Interface;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using SinglePlay;
using UnityEngine;

namespace MainGame.Extension
{
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private GameObject RangePrefab;


        private IBlockConfig _blockConfig;
        private ItemIdToBlockId _itemIdToBlockId;
        private PlayerInventoryViewModel _playerInventoryViewModel;
        private SelectHotBarControl _selectHotBarControl;
        private ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;

        
        private bool isBlockPlaceState;
        private List<GameObject> rangeObjects = new List<GameObject>();

        public void Construct(SinglePlayInterface singlePlayInterface,PlayerInventoryViewModel playerInventoryViewModel,SelectHotBarControl selectHotBarControl,UIStateControl uiStateControl,ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _blockConfig = singlePlayInterface.BlockConfig;
            _itemIdToBlockId = singlePlayInterface.ItemIdToBlockId;
            
            _playerInventoryViewModel = playerInventoryViewModel;
            _selectHotBarControl = selectHotBarControl;
            uiStateControl.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(UIStateEnum state)
        {
            if (isBlockPlaceState && state != UIStateEnum.BlockPlace)
            {
                isBlockPlaceState = false;
                return;
            }
            if (state != UIStateEnum.BlockPlace) return;
            
            isBlockPlaceState = true;

            if (!IsDisplay()) return;

            //電気系のブロックなので電柱の範囲を表示する
            


        }

        private bool IsDisplay()
        {
            var hotBarSlot = _selectHotBarControl.SelectIndex;
            var id = _playerInventoryViewModel[PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)].Id;

            if (id == ItemConst.EmptyItemId) return false;
            if (!_itemIdToBlockId.CanConvert(id)) return false;

            var blockConfig = _blockConfig.GetBlockConfig(_itemIdToBlockId.Convert(id));

            return IsElectricalBlock(blockConfig.Type);
        }

        private List<BlockGameObject> GetElectricalPoles()
        {
            var resultBlocks = new List<BlockGameObject>();
            foreach (var blocks in _chunkBlockGameObjectDataStore.BlockGameObjectDictionary)
            {
                var blockType = _blockConfig.GetBlockConfig(blocks.Value.BlockId).Type;
                if (blockType != VanillaBlockType.ElectricPole) continue;
                
                resultBlocks.Add(blocks.Value);
            }

            return resultBlocks;
        }

        //TODO 電気系のブロックかどうか判定するロジック
        private bool IsElectricalBlock(string type)
        {
            return type is VanillaBlockType.Generator or VanillaBlockType.Machine or VanillaBlockType.Miner or VanillaBlockType.ElectricPole;
        }
    }
}