using System;
using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
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
using VContainer;

namespace MainGame.Extension
{
    /// <summary>
    /// TODO 各データにアクセスしやすいようなアクセッサを作ってそっちに乗り換える
    /// </summary>
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private EnergizedRangeObject rangePrefab;


        private IBlockConfig _blockConfig;
        private ItemIdToBlockId _itemIdToBlockId;
        private PlayerInventoryViewModel _playerInventoryViewModel;
        private SelectHotBarControl _selectHotBarControl;
        private ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;

        
        private bool isBlockPlaceState;
        private readonly List<EnergizedRangeObject> rangeObjects = new();

        [Inject]
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
                foreach (var rangeObject in rangeObjects)
                {
                    Destroy(rangeObject.gameObject);
                }
                rangeObjects.Clear();
                return;
            }
            
            if (state != UIStateEnum.BlockPlace) return;
            isBlockPlaceState = true;

            
            var (isElectricalBlock,isPole) = IsDisplay();
            //電気ブロックでも電柱でもない
            if (!isElectricalBlock && !isPole) return;

            
            //電気系のブロックなので電柱の範囲を表示する
            foreach (var electricalPole in GetElectricalPoles())
            {
                var config = (ElectricPoleConfigParam)_blockConfig.GetBlockConfig(electricalPole.BlockId).Param;
                var range = isElectricalBlock ? config.machineConnectionRange : config.poleConnectionRange;

                var rangeObject = Instantiate(rangePrefab,electricalPole.transform.position,Quaternion.identity,transform);
                rangeObject.SetRange(range);
                rangeObjects.Add(rangeObject);
            }
        }

        private (bool isElectricalBlock, bool isPole) IsDisplay()
        {
            var hotBarSlot = _selectHotBarControl.SelectIndex;
            var id = _playerInventoryViewModel[PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)].Id;

            if (id == ItemConst.EmptyItemId) return (false,false);
            if (!_itemIdToBlockId.CanConvert(id)) return (false,false);

            var blockConfig = _blockConfig.GetBlockConfig(_itemIdToBlockId.Convert(id));

            return (IsElectricalBlock(blockConfig.Type),IsPole(blockConfig.Type));
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
            return type is VanillaBlockType.Generator or VanillaBlockType.Machine or VanillaBlockType.Miner;
        }
        private bool IsPole(string type){
            return type is VanillaBlockType.ElectricPole;
        }

    }
}