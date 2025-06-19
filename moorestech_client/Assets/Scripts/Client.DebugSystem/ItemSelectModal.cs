using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Mod.Texture;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.DebugSystem
{
    public class ItemSelectModal : MonoBehaviour
    {
        [SerializeField] private Transform itemSlotParent;
        [SerializeField] private Button closeButton;
        
        private List<ItemSlotView> _itemSlotObjects;
        private ItemSlotView selectedItemSlotView;
        
        public static ItemSelectModal Instance
        {
            get
            {
                if (_instance == null) { _instance = FindObjectOfType<ItemSelectModal>(true); }
                return _instance;
            }
        }
        
        private static ItemSelectModal _instance;
        
        public async UniTask<ItemViewData> SelectItem()
        {
            if (_itemSlotObjects == null)
            {
                Initialize();
            }
            
            gameObject.SetActive(true);
            
            var waitSelectItem = UniTask.WaitUntil(() => selectedItemSlotView != null);
            var waitClose = closeButton.OnClickAsync();
            await UniTask.WhenAny(waitSelectItem, waitClose);
            
            gameObject.SetActive(false);
            if (selectedItemSlotView == null)
            {
                return null;
            }
            
            var slotObject = selectedItemSlotView;
            selectedItemSlotView = null;
            
            return slotObject.ItemViewData;
        }
        
        private void Initialize()
        {
            _itemSlotObjects = new List<ItemSlotView>();
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var itemSlotObject = Instantiate(ItemSlotView.Prefab, itemSlotParent);
                
                itemSlotObject.SetItem(itemView, 0);
                itemSlotObject.OnRightClickUp.Subscribe(item => selectedItemSlotView = item).AddTo(this);
            }
        }
    }
}