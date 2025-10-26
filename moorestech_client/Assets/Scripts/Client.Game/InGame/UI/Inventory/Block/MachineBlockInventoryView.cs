using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MachineBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        [SerializeField] private RectTransform machineInputFluidParent;
        [SerializeField] private RectTransform machineOutputFluidParent;
        
        [SerializeField] private TMP_Text powerRateText;
        [SerializeField] private ProgressArrowView machineProgressArrow;
        [SerializeField] private TMP_Text machineRecipeCount;
        
        protected BlockGameObject BlockGameObject;
        
        private readonly List<FluidSlotView> _fluidSlotViews = new();
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            BlockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            
            // GearMachineParamとElectricMachineParamを共通して使える
            var param = blockGameObject.BlockMasterElement.BlockParam as IMachineParam;
            
            machineBlockNameText.text = blockGameObject.BlockMasterElement.Name;
            
            SetItemList();
            SetFluidList();
            
            #region Intenral
            
            void SetItemList()
            {
                for (var i = 0; i < param.InputSlotCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotView.Prefab, machineInputItemParent);
                    SubInventorySlotObjectsInternal.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
                
                for (var i = 0; i < param.OutputSlotCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotView.Prefab, machineOutputItemParent);
                    SubInventorySlotObjectsInternal.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
                
                UpdateItemList(itemList);
            }
            
            void SetFluidList()
            {
                for (var i = 0; i < param.InputTankCount; i++)
                {
                    var slotObject = Instantiate(FluidSlotView.Prefab, machineInputFluidParent);
                    _fluidSlotViews.Add(slotObject);
                }
                
                for (var i = 0; i < param.OutputTankCount; i++)
                {
                    var slotObject = Instantiate(FluidSlotView.Prefab, machineOutputFluidParent);
                    _fluidSlotViews.Add(slotObject);
                }
            }
            
  #endregion
        }
        
        protected void Update()
        {
            UpdateMachineRecipeView();
            UpdateMachineProgressArrow();
            UpdateFluidInventory();
            
            #region Internal
            
            void UpdateMachineRecipeView()
            {
                var state = BlockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
                if (state == null)
                {
                    return;
                }
                
                var machineRecipeCountText = string.Empty;
                if (state.MachineRecipeGuid != Guid.Empty.ToString())
                {
                    var recipeMaster = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(state.MachineRecipeGuid));
                    var minutesCount =  60.0f / recipeMaster.Time;
                    foreach (var item in recipeMaster.OutputItems)
                    {
                        var resultCount = item.Count * minutesCount;
                        var itemName = MasterHolder.ItemMaster.GetItemMaster(item.ItemGuid).Name;
                        machineRecipeCountText += $"{itemName} : {resultCount:F1}/分 ";
                    }
                }
                machineRecipeCount.text = machineRecipeCountText;
            }
            
            void UpdateMachineProgressArrow()
            {
                var state = BlockGameObject.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                if (state == null)
                {
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
            }
            
            void UpdateFluidInventory()
            {
                // GetStateDetailメソッドを使用して液体インベントリの状態を取得
                var fluidState = BlockGameObject.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
                if (fluidState == null)
                {
                    return;
                }
                
                var param = BlockGameObject.BlockMasterElement.BlockParam as IMachineParam;
                
                // 入力スロットの更新
                for (var i = 0; i < fluidState.InputTanks.Count; i++)
                {
                    var fluidInfo = fluidState.InputTanks[i];
                    var fluidId = new FluidId(fluidInfo.FluidId);
                    
                    var fluidView = fluidId == FluidMaster.EmptyFluidId ? null : ClientContext.FluidImageContainer.GetItemView(fluidId);
                    _fluidSlotViews[i].SetFluid(fluidView, fluidInfo.Amount);
                }
                
                // 出力スロットの更新
                var outputStartIndex = param.InputTankCount;
                for (var i = 0; i < fluidState.OutputTanks.Count; i++)
                {
                    var fluidInfo = fluidState.OutputTanks[i];
                    var fluidId = new FluidId(fluidInfo.FluidId);
                    var slotIndex = outputStartIndex + i;
                    
                    var fluidView = fluidId == FluidMaster.EmptyFluidId ? null : ClientContext.FluidImageContainer.GetItemView(fluidId);
                    _fluidSlotViews[slotIndex].SetFluid(fluidView, fluidInfo.Amount);
                }
            }
            
            #endregion
        }
    }
}