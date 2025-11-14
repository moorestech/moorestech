using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeViewManager : MonoBehaviour
    {
        [SerializeField] private ResearchTreeView researchTreeView;
        
        private CancellationToken _ct;

        [Inject]
        public void Construct(InitialHandshakeResponse initial)
        {
            _ct = this.GetCancellationTokenOnDestroy();
            
            // 研究完了ボタン押下時の処理登録
            // Register the process when the research complete button is clicked
            researchTreeView.OnClickResearchButton.Subscribe(node => CompleteResearchAsync(node).Forget()).AddTo(this);
            
            #region Internal
            
            async UniTask CompleteResearchAsync(ResearchNodeData node)
            {
                var guid = node.MasterElement.ResearchNodeGuid;
                var response = await ClientContext.VanillaApi.Response.CompleteResearch(guid, _ct);
                var nodes = CreateNodeData(response.NodeState.ToDictionary());
                
                researchTreeView.SetResearchNodes(nodes);
            }
            
            #endregion
        }

        // 研究ツリー状態の最新化
        // Refresh the research tree states
        private async UniTask LoadResearchTreeAsync()
        {
            var nodeStates = await ClientContext.VanillaApi.Response.GetResearchNodeStates(_ct);
            var nodes = CreateNodeData(nodeStates);

            researchTreeView.SetResearchNodes(nodes);
        }

        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
            
            if (!isActive) return;
            
            // 念の為の最新化処理
            // Refresh just in case
            LoadResearchTreeAsync().Forget();
        }
        
        
        /// <summary>
        /// サーバーから送られてきたデータを、使いやすい形に変換
        /// Convert the data received from the server into a more usable form
        /// </summary>
        private static List<ResearchNodeData> CreateNodeData(Dictionary<Guid, ResearchNodeState> nodeStates)
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
    }
}
