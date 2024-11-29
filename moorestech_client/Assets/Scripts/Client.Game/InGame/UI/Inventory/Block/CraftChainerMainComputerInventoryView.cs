using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class CraftChainerMainComputerInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform chestSlotsParent;
        
        private BlockGameObject _blockGameObject;
        private CancellationToken _gameObjectCancellationToken;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            
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
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
                UpdateItemList(itemList);
            }
            
  #endregion
        }
    }
}