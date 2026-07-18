using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using VContainer;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Game.InGame.Electric
{
    /// <summary>
    ///     電気系ブロック設置時に電柱の自動接続の探索範囲をボックスで表示する
    ///     Displays the pole's auto-connect search range as a box while placing an electric block
    /// </summary>
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private EnergizedRangeObject rangePrefab;
        private readonly List<EnergizedRangeObject> rangeObjects = new();
        
        [Inject] private BlockGameObjectDataStore _blockGameObjectDataStore;
        [Inject] private PlaceSystemStateController _placeSystemStateController;

        private bool isBlockPlaceState;
        private readonly ReactiveProperty<bool> _isRangeVisible = new(false);

        public IObservable<bool> OnRangeVisibleChanged => _isRangeVisible;
        public bool IsRangeVisible() => _isRangeVisible.Value;

        [Inject]
        public void Construct(UIStateControl uiStateControl)
        {
            uiStateControl.OnStateChanged += OnStateChanged;
            _blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock);
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
            _isRangeVisible.Value = false;
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
                
                // サーバの接続範囲ボックス(floor(range/2)起点)に合わせて中心をずらす。偶数は対称、奇数は+0.5セル
                // Offset the center to match the server's connection box (anchored at floor(range/2)): even = symmetric, odd = +0.5 cell
                var centerOffset = new Vector3(CellCenterOffset(horizontalRange), CellCenterOffset(heightRange), CellCenterOffset(horizontalRange));
                var rangeObject = Instantiate(rangePrefab, electricalPole.transform.position + centerOffset, Quaternion.identity, transform);
                rangeObject.SetRange(horizontalRange, heightRange);
                rangeObjects.Add(rangeObject);
            }
            _isRangeVisible.Value = rangeObjects.Count > 0;
            
            #region Internal

            // サーバの EnumerateRange は floor(range/2) を起点にするため、奇数レンジで中心が+0.5セルずれる
            // Server's EnumerateRange anchors at floor(range/2), so the center shifts +0.5 cell for odd ranges
            float CellCenterOffset(int range)
            {
                var clamped = Mathf.Max(range, 1);
                return clamped / 2f - clamped / 2;
            }

            (bool isElectricalBlock, bool isPole) IsDisplay()
            {
                // ビルドメニューでブロックを選択中のみ表示する（旧: 手持ちアイテムのブロック判定）
                // Show only while a block is selected in the build menu (was: held-item block check)
                if (_placeSystemStateController.CurrentTarget is not BlockPlacementTarget blockTarget) return (false, false);
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockId);

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
                return type is BlockTypeConst.ElectricGenerator or BlockTypeConst.ElectricMachine or BlockTypeConst.ElectricMiner or BlockTypeConst.GearToElectricGenerator or BlockTypeConst.ElectricPump or BlockTypeConst.ElectricToGearGenerator;
            }
            
            bool IsPole(string type)
            {
                return type is BlockTypeConst.ElectricPole;
            }
            
            #endregion
        }
    }
}
