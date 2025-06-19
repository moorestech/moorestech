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
        
        private List<ItemSlotObject> _itemSlotObjects;
        private ItemSlotObject _selectedItemSlotObject;
        
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
            
            var waitSelectItem = UniTask.WaitUntil(() => _selectedItemSlotObject != null);
            var waitClose = closeButton.OnClickAsync();
            await UniTask.WhenAny(waitSelectItem, waitClose);
            
            gameObject.SetActive(false);
            if (_selectedItemSlotObject == null)
            {
                return null;
            }
            
            var slotObject = _selectedItemSlotObject;
            _selectedItemSlotObject = null;
            
            return slotObject.ItemViewData;
        }
        
        private void Initialize()
        {
            _itemSlotObjects = new List<ItemSlotObject>();
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var itemSlotObject = Instantiate(ItemSlotObject.Prefab, itemSlotParent);
                
                itemSlotObject.SetItem(itemView, 0);
                itemSlotObject.OnRightClickUp.Subscribe(item => _selectedItemSlotObject = item).AddTo(this);
            }
        }
    }
}