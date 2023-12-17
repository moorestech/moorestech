using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Main
{
    public interface IInventoryItems : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
        public int Count { get; }
        public bool IsItemExist(string modId, string itemName);
    }
    
    public class InventoryMainAndSubCombineItems : IInventoryItems
    {
        private readonly List<IItemStack> _mainInventory;
        private readonly List<IItemStack> _subInventory;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        
        public InventoryMainAndSubCombineItems(SinglePlayInterface singlePlayInterface)
        {
            _itemConfig = singlePlayInterface.ItemConfig;
            _itemStackFactory = singlePlayInterface.ItemStackFactory;
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
            _subInventory = new List<IItemStack>();
        }
        
        public IEnumerator<IItemStack> GetEnumerator()
        {
            var merged = new List<IItemStack>();
            merged.AddRange(_mainInventory);
            merged.AddRange(_subInventory);
            return merged.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public void SetSubInventory(IReadOnlyList<IItemStack> subInventory)
        {
            _subInventory.Clear();
            _subInventory.AddRange(subInventory);
        }


        public int Count => _mainInventory.Count + _subInventory.Count;

        public bool IsItemExist(string modId, string itemName)
        {
            var id = _itemConfig.GetItemId(modId, itemName);
            return _mainInventory.Any(item => item.Id == id);
        }


        public IItemStack this[int index]
        {
            get
            {
                if (index < _mainInventory.Count) return _mainInventory[index];
                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.Count) return _subInventory[index - _mainInventory.Count];
                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " index:" + index);
                return _itemStackFactory.CreatEmpty();
            }
            set
            {
                if (index < _mainInventory.Count)
                {
                    _mainInventory[index] = value;
                    return;
                }

                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.Count)
                {
                    _subInventory[subIndex] = value;
                    return;
                }

                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " index:" + index);
            }
        }
    }
}