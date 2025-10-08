using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using VContainer;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Game.InGame.Electric
{
    /// <summary>
    ///     TODO 各データにアクセスしやすいようなアクセッサを作ってそっちに乗り換える
    /// </summary>
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private EnergizedRangeObject rangePrefab;
        private readonly List<EnergizedRangeObject> rangeObjects = new();
        
        [Inject] private BlockGameObjectDataStore _blockGameObjectDataStore;
        [Inject] private HotBarView _hotBarView;
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        
        private bool isBlockPlaceState;
        
        [Inject]
        public void Construct(UIStateControl uiStateControl)
        {
            _hotBarView.OnSelectHotBar += OnSelectHotBar;
            uiStateControl.OnStateChanged += OnStateChanged;
            _blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock);
        }
        
        private void OnSelectHotBar(int index)
        {
            ResetRangeObject();
            CreateRangeObject();
        }
        
        private void OnStateChanged(UIStateEnum state)
        {
            if (isBlockPlaceState && state != UIStateEnum.PlaceBlock)
            {
                isBlockPlaceState = false;
                ResetRangeObject();
                return;
            }
            
            if (state != UIStateEnum.PlaceBlock) return;
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
                var horizontalRange = isElectricalBlock
                    ? electricPoleParam.MachineConnectionRange
                    : electricPoleParam.PoleConnectionRange;
                var heightRange = isElectricalBlock
                    ? electricPoleParam.MachineConnectionHeightRange
                    : electricPoleParam.PoleConnectionHeightRange;
                
                var rangeObject = Instantiate(rangePrefab, electricalPole.transform.position, Quaternion.identity, transform);
                rangeObject.SetRange(horizontalRange, heightRange);
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
                    if (blockMaster.BlockType != BlockTypeConst.ElectricPole) continue;
                    
                    resultBlocks.Add(blocks.Value);
                }
                
                return resultBlocks;
            }
            
            //TODO 電気系のブロックかどうか判定するロジック
            bool IsElectricalBlock(string type)
            {
                return type is BlockTypeConst.ElectricGenerator or BlockTypeConst.ElectricMachine or BlockTypeConst.ElectricMiner or BlockTypeConst.GearElectricGenerator or BlockTypeConst.ElectricMiner;
            }
            
            bool IsPole(string type)
            {
                return type is BlockTypeConst.ElectricPole;
            }
            
            #endregion
        }
    }
}
