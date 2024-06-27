using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Core.Const;
using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using Game.Context;
using Game.PlayerInventory.Interface;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Electric
{
    /// <summary>
    ///     TODO 各データにアクセスしやすいようなアクセッサを作ってそっちに乗り換える
    /// </summary>
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private EnergizedRangeObject rangePrefab;
        private readonly List<EnergizedRangeObject> rangeObjects = new();
        
        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private HotBarView _hotBarView;
        private ILocalPlayerInventory _localPlayerInventory;
        
        private bool isBlockPlaceState;
        
        [Inject]
        public void Construct(HotBarView hotBarView, UIStateControl uiStateControl, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            
            _localPlayerInventory = localPlayerInventory;
            _hotBarView = hotBarView;
            
            hotBarView.OnSelectHotBar += OnSelectHotBar;
            uiStateControl.OnStateChanged += OnStateChanged;
            blockGameObjectDataStore.OnPlaceBlock += OnPlaceBlock;
        }
        
        private void OnSelectHotBar(int index)
        {
            ResetRangeObject();
            CreateRangeObject();
        }
        
        private void OnStateChanged(UIStateEnum state)
        {
            if (isBlockPlaceState && state != UIStateEnum.GameScreen)
            {
                isBlockPlaceState = false;
                ResetRangeObject();
                return;
            }
            
            if (state != UIStateEnum.GameScreen) return;
            isBlockPlaceState = true;
            
            CreateRangeObject();
        }
        
        private void OnPlaceBlock(BlockGameObject blockGameObject)
        {
            if (!isBlockPlaceState) return;
            
            ResetRangeObject();
            CreateRangeObject();
        }
        
        
        private void ResetRangeObject()
        {
            foreach (var rangeObject in rangeObjects) Destroy(rangeObject.gameObject);
            rangeObjects.Clear();
        }
        
        
        private void CreateRangeObject()
        {
            var blockConfig = ServerContext.BlockConfig;
            
            var (isElectricalBlock, isPole) = IsDisplay();
            //電気ブロックでも電柱でもない
            if (!isElectricalBlock && !isPole) return;
            
            
            //電気系のブロックなので電柱の範囲を表示する
            foreach (var electricalPole in GetElectricalPoles())
            {
                var config = (ElectricPoleConfigParam)blockConfig.GetBlockConfig(electricalPole.BlockId).Param;
                var range = isElectricalBlock ? config.machineConnectionRange : config.poleConnectionRange;
                
                var rangeObject = Instantiate(rangePrefab, electricalPole.transform.position, Quaternion.identity, transform);
                rangeObject.SetRange(range);
                rangeObjects.Add(rangeObject);
            }
            
            #region Internal
            
            (bool isElectricalBlock, bool isPole) IsDisplay()
            {
                var hotBarSlot = _hotBarView.SelectIndex;
                var id = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)].Id;
                
                if (id == ItemConst.EmptyItemId) return (false, false);
                if (!blockConfig.IsBlock(id)) return (false, false);
                
                var config = blockConfig.GetBlockConfig(blockConfig.ItemIdToBlockId(id));
                
                return (IsElectricalBlock(config.Type), IsPole(config.Type));
            }
            
            List<BlockGameObject> GetElectricalPoles()
            {
                var resultBlocks = new List<BlockGameObject>();
                foreach (var blocks in _blockGameObjectDataStore.BlockGameObjectDictionary)
                {
                    var blockType = blockConfig.GetBlockConfig(blocks.Value.BlockId).Type;
                    if (blockType != VanillaBlockType.ElectricPole) continue;
                    
                    resultBlocks.Add(blocks.Value);
                }
                
                return resultBlocks;
            }
            
            //TODO 電気系のブロックかどうか判定するロジック
            bool IsElectricalBlock(string type)
            {
                return type is VanillaBlockType.ElectricGenerator or VanillaBlockType.ElectricMachine or VanillaBlockType.ElectricMiner;
            }
            
            bool IsPole(string type)
            {
                return type is VanillaBlockType.ElectricPole;
            }
            
            #endregion
        }
    }
}