using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Research;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeViewManager : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private ResearchTreeView researchTreeView;
        
        public List<IItemStack> SubInventory { get; } = new();
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; private set; }
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
        public int Count => 0;
        
        private CancellationToken _destroyCancellationToken;

        public void Initialize(BlockGameObject blockGameObject)
        {
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(
                ItemMoveInventoryType.SubInventory,
                InventoryIdentifierMessagePack.CreateBlockMessage(blockGameObject.BlockPosInfo.OriginalPos));
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
            
            // 研究実行イベントの登録
            // Register research execution event
            researchTreeView.OnClickResearchButton.Subscribe(node => ResearchComplete(node).Forget()).AddTo(this);
            
            // 研究ツリーの読み込み
            // Load research tree
            LoadResearchTree().Forget();

            #region Internal

            // 研究ツリーの読み込み
            // Load research tree
            async UniTask LoadResearchTree()
            {
                var nodeStates = await ClientContext.VanillaApi.Response.GetResearchNodeStates(_destroyCancellationToken);
                var nodes = CreateNodeData(nodeStates);
                
                researchTreeView.SetResearchNodes(nodes);
            }
            
            // 研究完了処理
            // Research completion processing
            async UniTask ResearchComplete(ResearchNodeData node)
            {
                var guid = node.MasterElement.ResearchNodeGuid;
                var response = await ClientContext.VanillaApi.Response.CompleteResearch(guid, _destroyCancellationToken);
                var nodes = CreateNodeData(response.NodeState.ToDictionary());
                
                researchTreeView.SetResearchNodes(nodes);
            }
            
            // ノードデータの生成Util
            // Node data generation Util
            List<ResearchNodeData> CreateNodeData(Dictionary<Guid, ResearchNodeState> nodeStates)
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
        
        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI() 
        {
            Destroy(gameObject);
        }
    }
}
