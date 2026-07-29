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
        // 素手を表す選択インデックス。サーバーのEquipmentInventoryDataと同じ特別値
        // Selection index meaning bare hands; the same special value the server's EquipmentInventoryData uses
        public const int BareHandsIndex = -1;

        public IReadOnlyList<IItemStack> Slots => _slots;
        public int SelectedIndex { get; private set; }

        /// <summary>
        ///     選択中スロットの中身。選択中スロットの中身が変わってもselectedイベントは飛ばないため、
        ///     キャッシュせずスロット配列と選択インデックスから都度導出する。
        ///     The item currently held. A selected event is not dispatched when the selected slot's content changes,
        ///     so this is derived from the slot list and the selected index every time instead of being cached.
        /// </summary>
        public IItemStack SelectedItem => SelectedIndex < 0 ? ServerContext.ItemStackFactory.CreatEmpty() : _slots[SelectedIndex];

        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        private readonly List<IItemStack> _slots = new();

        public LocalPlayerEquipment()
        {
            // スロット数はサーバーと同じマスタ由来のため固定長で確保する
            // The slot count comes from the same master as the server, so the list is allocated at a fixed length
            var itemStackFactory = ServerContext.ItemStackFactory;
            for (var slot = 0; slot < MasterHolder.ToolMaster.EquipmentSlotCount; slot++) _slots.Add(itemStackFactory.CreatEmpty());

            // 初期データ到着までは素手。実値はApplyInitialが上書きする
            // Bare hands until the initial data arrives; ApplyInitial overwrites it with the real value
            SelectedIndex = BareHandsIndex;
        }

        /// <summary>
        ///     選択スロットを変更する。サーバーは変化時のみイベントを返すため応答は待たずローカルへ即時反映する。
        ///     Changes the selected slot. The server notifies only on change, so this applies locally at once without awaiting anything.
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            var clamped = ClampIndex(index);

            // 同値でも必ず送る。サーバーは変化時のみ返すため、一度ズレると同値送信の握り潰しでズレが恒久化する
            // Always send, even for an unchanged value: the server replies only on change, so swallowing same-value sends makes a desync permanent
            SelectedIndex = clamped;
            _onChanged.OnNext(Unit.Default);
            ClientContext.VanillaApi.SendOnly.SetSelectedEquipment(clamped);
        }

        // 以下のApply系はサーバー購読・初期データ適用の入口で、スロット更新は移動の楽観更新にも使う
        // The Apply methods below are the entry points for server subscriptions and initial data; the slot update also serves optimistic move writes

        public void ApplySlotUpdate(int slot, IItemStack itemStack)
        {
            if (slot < 0 || _slots.Count <= slot)
            {
                Debug.LogError("equipment slot out of range  slot:" + slot + " slotCount:" + _slots.Count);
                return;
            }

            _slots[slot] = itemStack;
            _onChanged.OnNext(Unit.Default);
        }

        public void ApplySelected(int index)
        {
            SelectedIndex = ClampIndex(index);
            _onChanged.OnNext(Unit.Default);
        }

        public void ApplyInitial(IReadOnlyList<IItemStack> equipmentSlots, int selectedIndex)
        {
            // マスタが示すスロット数を正とし、応答が足りない分は空で埋める
            // The master slot count wins; slots the response does not cover are filled with empty stacks
            var itemStackFactory = ServerContext.ItemStackFactory;
            for (var slot = 0; slot < _slots.Count; slot++)
            {
                _slots[slot] = slot < equipmentSlots.Count ? equipmentSlots[slot] : itemStackFactory.CreatEmpty();
            }

            SelectedIndex = ClampIndex(selectedIndex);
            _onChanged.OnNext(Unit.Default);
        }

        private int ClampIndex(int index)
        {
            // 素手(-1)から末尾スロットまでに丸める。サーバーのクランプ範囲と一致させる
            // Clamp between bare hands (-1) and the last slot, matching the server's clamp range
            return Math.Clamp(index, BareHandsIndex, _slots.Count - 1);
        }
    }
}
