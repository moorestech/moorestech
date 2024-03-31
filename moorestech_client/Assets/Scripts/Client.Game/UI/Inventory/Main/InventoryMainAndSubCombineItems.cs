using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.Context;
using Core.Item;
using Game.PlayerInventory.Interface;
using UniRx;
using UnityEngine;

namespace Client.Game.UI.Inventory.Main
{
    public interface ILocalPlayerInventory : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
        public IObservable<int> OnItemChange { get; }

        public int Count { get; }
        public bool IsItemExist(string modId, string itemName, int itemSlot);
    }

    /// <summary>
    ///     メインインベントリとサブインベントリを統合して扱えるローカルのプレイヤーインベントリ
    /// </summary>
    public class LocalPlayerInventory : ILocalPlayerInventory
    {
        private readonly List<IItemStack> _mainInventory;
        private readonly Subject<int> _onItemChange = new();

        private ISubInventory _subInventory;

        public LocalPlayerInventory()
        {
            _mainInventory = new List<IItemStack>();

            var itemStackFactory = MoorestechContext.ServerServices.ItemStackFactory;
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(itemStackFactory.CreatEmpty());
            }

            _subInventory = new EmptySubInventory();
        }
        public IObservable<int> OnItemChange => _onItemChange;

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


        public int Count => _mainInventory.Count + _subInventory.Count;

        public bool IsItemExist(string modId, string itemName, int itemSlot)
        {
            var id = MoorestechContext.ServerServices.ItemConfig.GetItemId(modId, itemName);

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
                return MoorestechContext.ServerServices.ItemStackFactory.CreatEmpty();
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

        public void SetSubInventory(ISubInventory subInventory)
        {
            _subInventory = subInventory;
        }
    }
}