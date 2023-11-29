using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using MainGame.UnityView.UI.UIObjects;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class PlayerInventorySlots : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> mainInventorySlots;
        private Vector2Int _blockPosition;


        private bool _isBlockInventoryOpening;


        /// <summary>
        ///     サブインベントリのスロットだけ
        /// </summary>
        private List<UIBuilderItemSlotObject> _subInventorySlots = new();


        private void Awake()
        {
            //メインインベントリのスロットのイベント登録
            mainInventorySlots.Select((slot, index) => new { slot, index }).ToList().ForEach(slot =>
            {
                slot.slot.OnRightClickDown += _ => OnRightClickDown?.Invoke(slot.index);
                slot.slot.OnLeftClickDown += _ => OnLeftClickDown?.Invoke(slot.index);
                slot.slot.OnRightClickUp += _ => OnRightClickUp?.Invoke(slot.index);
                slot.slot.OnLeftClickUp += _ => OnLeftClickUp?.Invoke(slot.index);
                slot.slot.OnCursorEnter += _ => OnCursorEnter?.Invoke(slot.index);
                slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slot.index);
                slot.slot.OnCursorMove += _ => OnCursorMove?.Invoke(slot.index);
                slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slot.index);
            });
        }

        public event Action<int> OnRightClickDown;
        public event Action<int> OnLeftClickDown;

        public event Action<int> OnRightClickUp;
        public event Action<int> OnLeftClickUp;
        public event Action<int> OnCursorEnter;
        public event Action<int> OnCursorExit;
        public event Action<int> OnCursorMove;
        public event Action<int> OnDoubleClick;
        public event Action<SubInventoryOptions> OnSetSubInventory;

        public void SetImage(int slot, ItemViewData itemView, int count)
        {
            if (slot < mainInventorySlots.Count)
                mainInventorySlots[slot].SetItem(itemView, count);
            else if (slot - mainInventorySlots.Count < _subInventorySlots.Count) _subInventorySlots[slot - mainInventorySlots.Count].SetItem(itemView, count);
        }


        /// <summary>
        ///     ブロックの状態を表示する機能(プログレスバーの矢印の表示など）
        /// </summary>
        public void SetBlockState(BlockStateChangeProperties stateChangeProperties, string blockType, Vector2Int blockPos)
        {
            //ブロックを開いてなかったらスルー
            if (!_isBlockInventoryOpening) return;
            //開いているブロックじゃなければスルー
            if (_blockPosition != blockPos) return;

            //Machine以外は実装上表示することがないのでスルー
            //TODo 今後タイプが増えたら抽象化する
            if (blockType != VanillaBlockType.Machine && blockType != VanillaBlockType.Miner) return;


            //⚠️ ここから下はMachine、Minerのみの処理
            
            //TODO プログレスバーの設定をする

        }
    }
}