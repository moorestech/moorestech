using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MachineBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        [SerializeField] private TMP_Text powerRateText;
        [SerializeField] private ProgressArrowView machineProgressArrow;
        
        protected BlockGameObject BlockGameObject;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            BlockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            
            // GearMachineParamとElectricMachineParamを共通して使える
            var param = blockGameObject.BlockMasterElement.BlockParam as IMachineParam;
            
            
            for (var i = 0; i < param.InputSlotCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, machineInputItemParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            for (var i = 0; i < param.OutputSlotCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, machineOutputItemParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            machineBlockNameText.text = blockGameObject.BlockMasterElement.Name;
            UpdateItemList(itemList);
        }
        
        protected void Update()
        {
            UpdateMachineProgressArrow();
            
            #region Internal
            
            void UpdateMachineProgressArrow()
            {
                var state = BlockGameObject.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                if (state == null)
                {
                    Debug.LogError("CommonMachineBlockStateDetailが取得できません。");
                    return;
                }
                
                var rate = state.ProcessingRate;
                machineProgressArrow.SetProgress(rate);
                
                var powerRate = state.PowerRate;
                var requiredPower = state.RequestPower;
                var currentPower = state.CurrentPower;
                
                var colorTag = powerRate < 1.0f ? "<color=red>" : string.Empty;
                var resetTag = powerRate < 1.0f ? "</color>" : string.Empty;
                
                powerRateText.text = $"エネルギー {colorTag}{powerRate * 100:F2}{resetTag}% {colorTag}{currentPower:F2}{resetTag}/{requiredPower:F2}";

                
                if (state == null)
                {
                    Debug.LogError("CommonMachineBlockStateが取得できませんでした。");
                }
            }
            
            #endregion
        }
    }
}