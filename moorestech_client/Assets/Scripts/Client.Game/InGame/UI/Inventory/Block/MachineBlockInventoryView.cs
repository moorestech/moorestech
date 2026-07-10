using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Block.RecipeSelection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.State;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Block
{
    [RequireComponent(typeof(MachineRecipeSelectionView))]
    public class MachineBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private RectTransform machineModuleItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        [SerializeField] private RectTransform machineInputFluidParent;
        [SerializeField] private RectTransform machineOutputFluidParent;
        
        [SerializeField] private TMP_Text powerRateText;
        [SerializeField] private ProgressArrowView machineProgressArrow;
        [SerializeField] private TMP_Text machineRecipeCount;

        // 電力機械プレハブでのみ配線する。歯車機械(継承先プレハブ)では未配線なのでnull許容
        // Wired only on electric-machine prefabs; left unwired (null) on gear-machine prefabs that inherit this view
        [SerializeField] private ElectricNetworkInfoView electricNetworkInfoView;

        protected BlockGameObject BlockGameObject;
        
        private readonly List<FluidSlotView> _fluidSlotViews = new();
        private MachineRecipeSelectionView _machineRecipeSelectionView;
        [Inject] private IGameUnlockStateData _gameUnlockStateData;
        
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

            // 電力機械プレハブでのみ電力ネットワーク情報を表示(歯車機械では未配線)
            // Show electric network info only on electric-machine prefabs (unwired on gear machines)
            if (electricNetworkInfoView != null) electricNetworkInfoView.Initialize(BlockGameObject.BlockInstanceId);

            // 移行用レシピ選択Viewを既存表示へ接続
            // Attach the transitional recipe selector to the existing label
            _machineRecipeSelectionView = GetComponent<MachineRecipeSelectionView>();
            _machineRecipeSelectionView.Initialize(machineRecipeCount, blockGameObject, _gameUnlockStateData);

            #region Internal
            
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

                // モジュールスロットは統合インベントリの第3レンジ（入力→出力→モジュールの順序を維持）
                // Module slots form the third range of the unified inventory (keep input -> output -> module order)
                for (var i = 0; i < param.ModuleSlotCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotView.Prefab, machineModuleItemParent);
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
            _machineRecipeSelectionView.Refresh();
            UpdateMachineProgressArrow();
            UpdateFluidInventory();
            
            #region Internal
            
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
