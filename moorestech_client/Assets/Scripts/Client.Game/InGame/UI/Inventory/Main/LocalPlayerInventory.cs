using System;
using System.Collections;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public interface ILocalPlayerInventory : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
        public IObservable<int> OnItemChange { get; }

        public int Count { get; }
        public int MainSlotCount { get; }
        public bool IsItemExist(ItemId itemId, int itemSlot);
    }
    
    /// <summary>
    ///     メインインベントリとサブインベントリを統合して扱えるローカルのプレイヤーインベントリ
    /// </summary>
    public class LocalPlayerInventory : ILocalPlayerInventory
    {
        public IObservable<int> OnItemChange => _onItemChange;
        private readonly Subject<int> _onItemChange = new();
        
        public int Count => _mainInventory.Count + _subInventory.Count;
        public int MainSlotCount => _mainInventory.Count;

        private readonly List<IItemStack> _mainInventory;
        private ISubInventory _subInventory;

        public LocalPlayerInventory()
        {
            _mainInventory = new List<IItemStack>();

            // 初期サイズはレベル0のスロット数。実サイズはサーバーレスポンスで確定する
            // Initial size is the level-0 slot count; the real size comes from the server response
            var itemStackFactory = ServerContext.ItemStackFactory;
            var initialSlotCount = PlayerInventorySlotLevelMasterUtil.GetSlotCount(0);
            for (var i = 0; i < initialSlotCount; i++) _mainInventory.Add(itemStackFactory.CreatEmpty());

            _subInventory = new EmptySubInventory();
        }

        public void EnsureMainSlotCount(int slotCount)
        {
            // レベルアップ通知（範囲外スロットのイベント）で末尾に空スロットを足す
            // Grow with empty tail slots when an out-of-range slot event arrives after level up
            var itemStackFactory = ServerContext.ItemStackFactory;
            while (_mainInventory.Count < slotCount)
            {
                _mainInventory.Add(itemStackFactory.CreatEmpty());
                _onItemChange.OnNext(_mainInventory.Count - 1);
            }
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
        
        public bool IsItemExist(ItemId itemId, int itemSlot)
        {
            if (itemSlot < 0)
            {
                Debug.LogError("inventory index out of range  index:" + itemSlot);
                return false;
            }

            if (itemSlot < _mainInventory.Count) return _mainInventory[itemSlot].Id == itemId;
            
            var subIndex = itemSlot - _mainInventory.Count;
            if (subIndex < _subInventory.SubInventory.Count) return _subInventory.SubInventory[subIndex].Id == itemId;
            
            Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " SubInventoryDataCount:" + _subInventory.SubInventory.Count + " index:" + itemSlot);
            return false;
        }
        
        public IItemStack this[int index]
        {
            get
            {
                if (index < 0)
                {
                    Debug.LogError("inventory index out of range  index:" + index);
                    return ServerContext.ItemStackFactory.CreatEmpty();
                }

                if (index < _mainInventory.Count) return _mainInventory[index];
                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.SubInventory.Count) return _subInventory.SubInventory[subIndex];
                
                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " SubInventoryDataCount:" + _subInventory.SubInventory.Count + " index:" + index);
                return ServerContext.ItemStackFactory.CreatEmpty();
            }
            set
            {
                if (index < 0)
                {
                    Debug.LogError("inventory index out of range  index:" + index);
                    return;
                }

                if (index < _mainInventory.Count)
                {
                    _mainInventory[index] = value;
                    _onItemChange.OnNext(index);
                    return;
                }
                
                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.SubInventory.Count)
                {
                    _subInventory.SubInventory[subIndex] = value;
                    _onItemChange.OnNext(index);
                    return;
                }
                
                Debug.LogError("sub inventory index out of range  SubInventoryCount:" + _subInventory.Count + " SubInventoryDataCount:" + _subInventory.SubInventory.Count + " index:" + index);
            }
        }
        
        public void SetSubInventory(ISubInventory subInventory)
        {
            _subInventory = subInventory;
        }
        
        public void SetMainInventory(List<IItemStack> mainInventoryList)
        {
            _mainInventory.Clear();
            _mainInventory.AddRange(mainInventoryList);
        }
        
    }
}
