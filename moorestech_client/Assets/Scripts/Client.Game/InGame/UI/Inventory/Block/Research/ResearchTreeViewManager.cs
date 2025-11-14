using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeViewManager : MonoBehaviour
    {
        [SerializeField] private ResearchTreeView researchTreeView;
        
        private CancellationToken _destroyCancellationToken;
        private bool _isPrepared;

        private void Awake()
        {
            // 破棄まで利用できるトークンを取得
            // Acquire the cancellation token valid until destruction
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
        }

        /// <summary>
        /// UI表示の切り替え
        /// Toggle UI visibility
        /// </summary>
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);

            if (!isActive) return;

            // 表示時に最新状態へ更新
            // Refresh data when the UI is shown
            if (!_isPrepared)
            {
                researchTreeView.OnClickResearchButton.Subscribe(node => CompleteResearchAsync(node).Forget()).AddTo(this);
                _isPrepared = true;
            }

            LoadResearchTreeAsync().Forget();
        }

        #region Internal

        // 研究ツリー状態の最新化
        // Refresh the research tree states
        private async UniTask LoadResearchTreeAsync()
        {
            var nodeStates = await ClientContext.VanillaApi.Response.GetResearchNodeStates(_destroyCancellationToken);
            var nodes = CreateNodeData(nodeStates);

            researchTreeView.SetResearchNodes(nodes);
        }

        // 研究完了後の更新
        // Update tree after completing a research
        private async UniTask CompleteResearchAsync(ResearchNodeData node)
        {
            var guid = node.MasterElement.ResearchNodeGuid;
            var response = await ClientContext.VanillaApi.Response.CompleteResearch(guid, _destroyCancellationToken);
            var nodes = CreateNodeData(response.NodeState.ToDictionary());

            researchTreeView.SetResearchNodes(nodes);
        }

        // ノードデータ生成処理
        // Build node data list
        private List<ResearchNodeData> CreateNodeData(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            var researchMasters = MasterHolder.ResearchMaster.GetAllResearches();
            var nodes = new List<ResearchNodeData>(researchMasters.Count);
            foreach (var master in researchMasters)
            {
                var state = nodeStates.GetValueOrDefault(master.ResearchNodeGuid, ResearchNodeState.UnresearchableAllReasons);
                var node = new ResearchNodeData(master, state);
                nodes.Add(node);
            }

            return nodes;
        }

        #endregion
    }
}
