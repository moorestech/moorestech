using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using SinglePlay;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Main
{
    public interface IInventoryItems : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
        public IObservable<int> OnItemChange { get; }

        public int Count { get; }
        public bool IsItemExist(string modId, string itemName);
    }
    
    public class InventoryMainAndSubCombineItems : IInventoryItems
    {
        public IObservable<int> OnItemChange => _onItemChange;
        private readonly Subject<int> _onItemChange = new();

        private readonly List<IItemStack> _mainInventory;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        
        private ISubInventory _subInventory;

        public InventoryMainAndSubCombineItems(SinglePlayInterface singlePlayInterface)
        {
            _itemConfig = singlePlayInterface.ItemConfig;
            _itemStackFactory = singlePlayInterface.ItemStackFactory;
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }

            _subInventory = new EmptySubInventory();
        }
        
        public IEnumerator<IItemStack> GetEnumerator()
        {
            var merged = new List<IItemStack>();
            merged.AddRange(_mainInventory);
            merged.AddRange(_subInventory.SubInventory);
            return merged.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public void SetSubInventory(ISubInventory subInventory)
        {
            _subInventory = subInventory;
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
                if (subIndex < _subInventory.Count) return _subInventory.SubInventory[index - _mainInventory.Count];
                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " index:" + index);
                return _itemStackFactory.CreatEmpty();
            }
            set
            {
                if (index < _mainInventory.Count)
                {
                    _mainInventory[index] = value;
                    _onItemChange.OnNext(index);
                    return;
                }

                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.Count)
                {
                    _subInventory.SubInventory[subIndex] = value;
                    _onItemChange.OnNext(index);
                    return;
                }

                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " index:" + index);
            }
        }
    }
}