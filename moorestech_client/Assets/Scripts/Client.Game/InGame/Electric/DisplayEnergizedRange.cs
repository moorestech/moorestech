using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Core.Const;
using Core.Master;
using Game.Block;
using Game.Context;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;
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
            var (isElectricalBlock, isPole) = IsDisplay();
            //電気ブロックでも電柱でもない
            if (!isElectricalBlock && !isPole) return;
            
            //電気系のブロックなので電柱の範囲を表示する
            foreach (var electricalPole in GetElectricalPoles())
            {
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(electricalPole.BlockId);
                var electricPoleParam = (ElectricPoleBlockParam)blockMasterElement.BlockParam;
                var range = isElectricalBlock ? electricPoleParam.MachineConnectionRange : electricPoleParam.PoleConnectionRange;
                
                var rangeObject = Instantiate(rangePrefab, electricalPole.transform.position, Quaternion.identity, transform);
                rangeObject.SetRange(range);
                rangeObjects.Add(rangeObject);
            }
            
            #region Internal
            
            (bool isElectricalBlock, bool isPole) IsDisplay()
            {
                var hotBarSlot = _hotBarView.SelectIndex;
                var id = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)].Id;
                
                if (id == ItemMaster.EmptyItemId) return (false, false);
                

                if (!MasterHolder.BlockMaster.IsBlock(id)) return (false, false);
                
                var blockId = MasterHolder.BlockMaster.GetBlockId(id);
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                
                return (IsElectricalBlock(blockMaster.BlockType), IsPole(blockMaster.BlockType));
            }
            
            List<BlockGameObject> GetElectricalPoles()
            {
                var resultBlocks = new List<BlockGameObject>();
                foreach (var blocks in _blockGameObjectDataStore.BlockGameObjectDictionary)
                {
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blocks.Value.BlockId);
                    if (blockMaster.BlockType != VanillaBlockType.ElectricPole) continue;
                    
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