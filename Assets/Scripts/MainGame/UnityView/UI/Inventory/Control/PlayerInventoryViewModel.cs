using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using MainGame.Basic;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Control
{
    /// <summary>
    /// プレイヤーの操作に応じてローカルでもっておくインベントリのアイテムリスト
    /// 実際のインベントリとのラグを軽減するためのキャッシュ機構で、実際のインベントリを更新するパケットが到着したら適宜入れ替える（ロールバックを発生させる）
    /// </summary>
    public class PlayerInventoryViewModel : IEnumerable<IItemStack>
    {
        /// <summary>
        /// クライアント側のメインインベントリのキャッシュ
        /// シフトを押してアイテムを分割しておく時など、サーバーとの同期を取る前の段階でのインベントリの状態を保持する
        /// </summary>
        private readonly List<IItemStack> _mainInventory;
        public IReadOnlyList<IItemStack> MainInventory => _mainInventory;
        /// <summary>
        /// <see cref="_mainInventory"/>と同じ理由でキャッシュしている
        /// サブインベントリはクラフトの時もあればブロックの時もあって、直接セットする必要があるのでreadonlyにしていない
        /// </summary>
        private List<IItemStack> _subInventory = new();
        private readonly ItemStackFactory _itemStackFactory;
        public event Action OnInventoryUpdate;
        public int Count => _mainInventory.Count + _subInventory.Count;


        public PlayerInventoryViewModel(SinglePlayInterface single)
        {
            _itemStackFactory = single.ItemStackFactory;
            
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
        }

        public IItemStack this[int index]
        {
            get
            {
                if (index < _mainInventory.Count)
                {
                    return _mainInventory[index];
                }
                var subIndex = index - _mainInventory.Count;
                if (subIndex < _subInventory.Count)
                {
                    return _subInventory[index - _mainInventory.Count];
                }
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

        
        public void SetSubInventory(List<ItemStack> subInventory)
        {
            _subInventory = subInventory.ToIItemStackList(_itemStackFactory);
            OnInventoryUpdate?.Invoke();
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
    }
}

public static class ListItemStackExtend
{
    public static List<IItemStack> ToIItemStackList(this List<ItemStack> list, ItemStackFactory factory)
    {
        return list.Select(i => factory.Create(i.ID, i.Count)).ToList();
    }
}
