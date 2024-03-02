using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using ServerServiceProvider;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Main
{
    public interface ILocalPlayerInventory : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
        public IObservable<int> OnItemChange { get; }

        public int Count { get; }
        public bool IsItemExist(string modId, string itemName,int itemSlot);
    }
    
    /// <summary>
    /// メインインベントリとサブインベントリを統合して扱えるローカルのプレイヤーインベントリ
    /// </summary>
    public class LocalPlayerInventory : ILocalPlayerInventory
    {
        public IObservable<int> OnItemChange => _onItemChange;
        private readonly Subject<int> _onItemChange = new();

        private readonly List<IItemStack> _mainInventory;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        
        private ISubInventory _subInventory;

        public LocalPlayerInventory(MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            _itemConfig = moorestechServerServiceProvider.ItemConfig;
            _itemStackFactory = moorestechServerServiceProvider.ItemStackFactory;
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

        public bool IsItemExist(string modId, string itemName,int itemSlot)
        {
            var id = _itemConfig.GetItemId(modId, itemName);
            if (itemSlot < _mainInventory.Count) return _mainInventory[itemSlot].Id == id;
            var subIndex = itemSlot - _mainInventory.Count;
            if (subIndex < _subInventory.Count) return _subInventory.SubInventory[itemSlot - _mainInventory.Count].Id == id;
            Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " index:" + itemSlot);
            return false;
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