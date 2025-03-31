using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MinerBlockInventoryView : CommonBlockInventoryViewBase 
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform miningItemSlotParent;
        [SerializeField] private RectTransform minerResultsParent;
        
        [SerializeField] private ProgressArrowView minerProgressArrow;
        
        private BlockGameObject _blockGameObject;
        private CancellationToken _gameObjectCancellationToken;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            _blockGameObject = blockGameObject;
            
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
                    var slotObject = Instantiate(itemSlotObjectPrefab, minerResultsParent);
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
                // ここが重かったら検討
                var commonProcessor = (CommonMachineBlockStateChangeProcessor)_blockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as CommonMachineBlockStateChangeProcessor);
                if (commonProcessor == null) return;
                
                minerProgressArrow.SetProgress(commonProcessor.CurrentMachineState?.ProcessingRate ?? 0.0f);
            }
            
  #endregion
        }
        
        private async UniTask SetMiningItem()
        {
            // 採掘中のアイテムを取得
            var pos = _blockGameObject.BlockPosInfo.OriginalPos;
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
                var slot = Instantiate(itemSlotObjectPrefab, miningItemSlotParent);
                slot.SetItem(itemView, 0);
            }
        } 
    }
}