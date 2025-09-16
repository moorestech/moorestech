using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
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
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, blockGameObject.BlockPosInfo.OriginalPos);
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
            LoadResearchTree().Forget();

            #region Internal

            async UniTask LoadResearchTree()
            {
                var completedResearchGuids = await ClientContext.VanillaApi.Response.GetCompletedResearchGuids(_destroyCancellationToken);
                var completedResearchSet = new HashSet<Guid>(completedResearchGuids);

                var researchMasters = MasterHolder.ResearchMaster.GetAllResearches();
                var nodes = new List<ResearchNodeData>(researchMasters.Count);
                foreach (var master in researchMasters)
                {
                    var node = new ResearchNodeData(master, completedResearchSet.Contains(master.ResearchNodeGuid));
                    nodes.Add(node);
                }

                researchTreeView.SetResearchNodes(nodes);
            }

  #endregion
        }
        
        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI() { }
    }
}
