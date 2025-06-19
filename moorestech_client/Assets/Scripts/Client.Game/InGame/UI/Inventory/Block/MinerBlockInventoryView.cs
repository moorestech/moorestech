using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MinerBlockInventoryView : CommonBlockInventoryViewBase 
    {
        [SerializeField] private RectTransform miningItemSlotParent;
        [SerializeField] private RectTransform minerResultsParent;
        
        [SerializeField] private TMP_Text powerRateText;
        [SerializeField] private ProgressArrowView minerProgressArrow;
        
        protected BlockGameObject BlockGameObject;
        private CancellationToken _gameObjectCancellationToken;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            BlockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var outputCount = param switch
            {
                ElectricMinerBlockParam blockParam => blockParam.OutputItemSlotCount, // TODO master interfaceブロックインベントリの整理箇所
                GearMinerBlockParam blockParam => blockParam.OutputItemSlotCount,
                _ => 0
            };
            
            // リザルトアイテムスロットを作成
            CreateResultItemSlot();
            
            // アイテムリストを更新
            UpdateItemList(itemList);
            
            // 採掘アイテムスロットを更新
            SetMiningItem().Forget();
            
            #region Internal
            
            void CreateResultItemSlot()
            {
                for (var i = 0; i < outputCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotObject.Prefab, minerResultsParent);
                    SubInventorySlotObjectsInternal.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
            }
            
  #endregion
        }
        
        protected void Update()
        {
            UpdateMinerProgressArrow();
            
            #region Internal
            
            void UpdateMinerProgressArrow()
            {
                var state = BlockGameObject.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                if (state == null)
                {
                    Debug.LogError("CommonMachineBlockStateDetailが取得できません。");
                    return;
                }
                
                var rate = state.ProcessingRate;
                minerProgressArrow.SetProgress(rate);
                
                var powerRate = state.PowerRate;
                var requiredPower = state.RequestPower;
                var currentPower = state.CurrentPower;
                
                var colorTag = powerRate < 1.0f ? "<color=red>" : string.Empty;
                var resetTag = powerRate < 1.0f ? "</color>" : string.Empty;
                
                powerRateText.text = $"エネルギー {colorTag}{powerRate * 100:F2}{resetTag}% {colorTag}{currentPower:F2}{resetTag}/{requiredPower:F2}";
            }
            
  #endregion
        }
        
        private async UniTask SetMiningItem()
        {
            // 採掘中のアイテムを取得
            var pos = BlockGameObject.BlockPosInfo.OriginalPos;
            var blockStates = await ClientContext.VanillaApi.Response.GetBlockState(pos, _gameObjectCancellationToken);
            if (blockStates == null)
            {
                Debug.LogError("ステートが取得できませんでした。");
                return;
            }
            
            var state = blockStates.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                Debug.LogError("CommonMinerのステートが取得できませんでした。");
                return;
            }
            
            // 採掘中のアイテムを表示
            foreach (var itemId in state.GetCurrentMiningItemIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var slot = Instantiate(ItemSlotObject.Prefab, miningItemSlotParent);
                slot.SetItem(itemView, 0);
            }
        } 
    }
}