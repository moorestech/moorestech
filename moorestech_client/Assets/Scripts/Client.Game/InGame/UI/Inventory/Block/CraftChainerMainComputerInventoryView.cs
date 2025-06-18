using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class CraftChainerMainComputerInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform chestSlotsParent;
        
        [SerializeField] private Button requestButton;
        [SerializeField] private CraftChainerMainComputerSelectRequestItemModal selectRequestItemModal;
        
        private BlockGameObject _blockGameObject;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            selectRequestItemModal.Initialize();
            requestButton.onClick.AddListener(() => OnClickRequestButton().Forget());
            
            // アイテムリストを初期化
            // Initialize item list
            InitializeItemList();
            
            #region Internal
            
            void InitializeItemList()
            {
                var itemList = new List<IItemStack>();
                var param = (CraftChainerMainComputerBlockParam)blockGameObject.BlockMasterElement.BlockParam;
                for (var i = 0; i < param.ItemSlotCount; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                    SubInventorySlotObjectsInternal.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
                UpdateItemList(itemList);
            }
            
  #endregion
        }
        
        private async UniTask OnClickRequestButton()
        {
            var (itemId, count) = await selectRequestItemModal.GetRequestItem();
            if (itemId == ItemMaster.EmptyItemId) return;
            
            var pos = _blockGameObject.BlockPosInfo.OriginalPos;
            ClientContext.VanillaApi.SendOnly.SetCraftChainerMainComputerRequestItem(pos, itemId, count);
        }
    }
}