// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MinerBlockInventoryView : CommonBlockInventoryViewBase 
    {
        [SerializeField] private RectTransform miningItemSlotParent;
        [SerializeField] private RectTransform minerResultsParent;
        
        [SerializeField] private TMP_Text powerRateText;
        [SerializeField] private ProgressArrowView minerProgressArrow;
        
        [SerializeField] private TMP_Text miningItemCount;
        
        protected BlockGameObject BlockGameObject;
        private CancellationToken _gameObjectCancellationToken;
        private readonly List<(System.Guid itemGuid, float perMinute)> _visibleMiningItems = new();
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            BlockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            var outputCount = ((IMinerParam) blockGameObject.BlockMasterElement.BlockParam).OutputItemSlotCount; 
            
            // リザルトアイテムスロットを作成
            CreateResultItemSlot();
            
            // アイテムリストを更新
            UpdateItemList(itemList);
            
            // 採掘アイテムスロットを更新
            SetMiningItem().Forget();
            Localize.OnLanguageChanged.Subscribe(_ => RefreshMiningItemCount()).AddTo(this);
            
            #region Internal
            
            void CreateResultItemSlot()
            {
                for (var i = 0; i < outputCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotView.Prefab, minerResultsParent);
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
            // Fetch currently mining items
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
            
            var currentMiningItemIds = state.GetCurrentMiningItemIds();
            
            // 採掘中のアイテムを表示
            // Render the currently mining items
            foreach (var itemId in currentMiningItemIds)
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var slot = Instantiate(ItemSlotView.Prefab, miningItemSlotParent);
                slot.SetItem(itemView, 0);
            }
            
            var mineSettings = ((IMinerParam) BlockGameObject.BlockMasterElement.BlockParam).MineSettings;
            _visibleMiningItems.Clear();
            foreach (var settings in mineSettings.items)
            {
                // 分間採掘数はサーバーの実効採掘時間から出す。跨いだ鉱脈は最も遅い1本のタイマーを共有する
                // The per-minute count comes from the server's effective mining time; straddled veins share one timer, the slowest
                var targetItemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                if (!currentMiningItemIds.Contains(targetItemId)) continue;
                if (state.MiningSeconds <= 0) continue;
                
                var perMinute = (float)(60 / state.MiningSeconds);
                _visibleMiningItems.Add((settings.ItemGuid, perMinute));
            }
            RefreshMiningItemCount();
        }

        private void RefreshMiningItemCount()
        {
            var text = string.Empty;
            foreach (var (itemGuid, perMinute) in _visibleMiningItems)
            {
                var itemName = Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuid));
                text += $"{itemName} : {perMinute:F1}/分 ";
            }
            miningItemCount.text = text;
        }
    }
}
