using System.Collections.Generic;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Train
{
    public class TrainInventoryView : MonoBehaviour, ITrainInventoryView
    {
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => _subInventorySlotObjects;
        public int Count => _subInventorySlotObjects.Count;
        public List<IItemStack> SubInventory { get; } = new();
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; protected set; }
        
        
        [SerializeField] private Transform slotParentTransform;
        private readonly List<ItemSlotView> _subInventorySlotObjects = new();
        
        
        public void Initialize(TrainCarEntityObject trainCarEntity)
        {
            ISubInventoryIdentifier = new TrainInventorySubInventoryIdentifier(trainCarEntity.TrainCarId);
            for (int i = 0; i < trainCarEntity.TrainCarMasterElement.InventorySlots; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, slotParentTransform);
                _subInventorySlotObjects.Add(slotObject);
            }
        }
        
        public void UpdateItemList(List<IItemStack> response)
        {
            SubInventory.Clear();
            SubInventory.AddRange(response);
        }
        
        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                //TODO ログ基盤にいれる
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }
            
            SubInventory[slot] = item;
        }
        
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}