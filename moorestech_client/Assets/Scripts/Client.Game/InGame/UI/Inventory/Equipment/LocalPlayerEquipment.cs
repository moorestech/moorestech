using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Equipment
{
    /// <summary>
    ///     サーバー権威の装備インベントリをクライアントへ写したモデル
    ///     Client-side mirror of the server-authoritative equipment inventory
    /// </summary>
    public class LocalPlayerEquipment
    {
        public IReadOnlyList<IItemStack> Slots => _slots;
        public int SelectedIndex { get; private set; }

        // サーバー確定値が届くたびに進む世代番号。WebUIが楽観選択を確定へ収束させる判定に使う
        // Revision that advances on every server-confirmed value; the web UI converges optimistic selection with it
        public int SelectionConfirmationRevision { get; private set; }

        /// <summary>
        ///     選択中スロットの中身。選択中スロットの中身が変わってもselectedイベントは飛ばないため、
        ///     キャッシュせずスロット配列と選択インデックスから都度導出する。
        ///     The item currently held. A selected event is not dispatched when the selected slot's content changes,
        ///     so this is derived from the slot list and the selected index every time instead of being cached.
        /// </summary>
        public IItemStack SelectedItem => _slots[SelectedIndex];

        // スロット内容の更新と選択スロットの変更のどちらでも発火する
        // Fires both on slot content updates and on selected-slot changes
        public IObservable<Unit> OnSlotsOrSelectionChanged => _onSlotsOrSelectionChanged;
        private readonly Subject<Unit> _onSlotsOrSelectionChanged = new();

        private readonly List<IItemStack> _slots = new();

        public LocalPlayerEquipment()
        {
            // スロット数はサーバーと同じマスタ由来のため固定長で確保する
            // The slot count comes from the same master as the server, so the list is allocated at a fixed length
            var itemStackFactory = ServerContext.ItemStackFactory;
            for (var slot = 0; slot < MasterHolder.ItemMaster.Items.EquipmentSlotCount; slot++) _slots.Add(itemStackFactory.CreatEmpty());

            // 初期データ到着までは先頭スロット。実値はInitializeが上書きする
            // The first slot until the initial data arrives; Initialize overwrites it with the real value
            SelectedIndex = 0;
        }

        public void Initialize(IReadOnlyList<IItemStack> equipmentSlots, int selectedIndex)
        {
            // マスタが示すスロット数を正とし、応答が足りない分は空で埋める
            // The master slot count wins; slots the response does not cover are filled with empty stacks
            var itemStackFactory = ServerContext.ItemStackFactory;
            for (var slot = 0; slot < _slots.Count; slot++)
            {
                _slots[slot] = slot < equipmentSlots.Count ? equipmentSlots[slot] : itemStackFactory.CreatEmpty();
            }

            SelectedIndex = ClampIndex(selectedIndex);
            SelectionConfirmationRevision++;
            _onSlotsOrSelectionChanged.OnNext(Unit.Default);
        }

        /// <summary>
        ///     装備選択を即時反映し、サーバー確定値へ収束させる。
        ///     Optimistically selects equipment, then converges on the server value.
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            var clamped = ClampIndex(index);

            // 同値でも必ず送り、サーバーの無条件エコーで確定させる
            // Always send equal values too, letting the server's unconditional echo confirm them
            SelectedIndex = clamped;
            _onSlotsOrSelectionChanged.OnNext(Unit.Default);
            ClientContext.VanillaApi.SendOnly.SetSelectedEquipment(clamped);
        }

        // 以下のApply系はサーバー購読の入口で、スロット更新は移動の楽観更新にも使う
        // The Apply methods below are the entry points for server subscriptions; the slot update also serves optimistic move writes

        public void ApplySlotUpdate(int slot, IItemStack itemStack)
        {
            if (slot < 0 || _slots.Count <= slot)
            {
                Debug.LogError("equipment slot out of range  slot:" + slot + " slotCount:" + _slots.Count);
                return;
            }

            _slots[slot] = itemStack;
            _onSlotsOrSelectionChanged.OnNext(Unit.Default);
        }

        public void ApplySelected(int index)
        {
            SelectedIndex = ClampIndex(index);
            SelectionConfirmationRevision++;
            _onSlotsOrSelectionChanged.OnNext(Unit.Default);
        }

        private int ClampIndex(int index)
        {
            // 先頭から末尾スロットまでに丸める。サーバーのクランプ範囲と一致させる
            // Clamp between the first and the last slot, matching the server's clamp range
            return Math.Clamp(index, 0, _slots.Count - 1);
        }
    }
}
