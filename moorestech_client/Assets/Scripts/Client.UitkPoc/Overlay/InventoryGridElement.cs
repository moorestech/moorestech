using System.Collections.Generic;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // ゴースト付きのドラッグ＆ドロップと右クリックメニューを持つインベントリ。ランタイムUITKにはHTML5 DnD相当が無いのでポインタ捕捉で組む
    // Inventory with ghost drag-and-drop and a context menu; runtime UITK lacks HTML5 DnD, so it is built on pointer capture
    public class InventoryGridElement : VisualElement
    {
        private const float DragThreshold = 4f;

        private readonly List<InventorySlotElement> _slots = new();
        private readonly VisualElement _overlay;
        private readonly TooltipLayer _tooltips;
        private readonly VisualElement _ghost = new();
        private InventorySlotElement _source;
        private InventorySlotElement _hovered;
        private Vector2 _pressPosition;
        private bool _dragging;

        public InventoryGridElement(UitkPocContext context, VisualElement overlay, TooltipLayer tooltips, List<(string name, int count)> contents)
        {
            AddToClassList("ov-grid");
            _overlay = overlay;
            _tooltips = tooltips;
            _ghost.AddToClassList("ov-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            for (var i = 0; i < contents.Count; i++)
            {
                var slot = new InventorySlotElement(context, i);
                slot.SetContent(contents[i].name, contents[i].count);
                tooltips.Attach(slot);
                _slots.Add(slot);
                Add(slot);
            }
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var slot = SlotAt(evt.position);
            if (slot == null) return;
            if (evt.button == 1)
            {
                OpenContextMenu(slot, evt.position);
                return;
            }
            if (slot.ItemName == null) return;
            _source = slot;
            _pressPosition = evt.position;
            this.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_source == null || !this.HasPointerCapture(evt.pointerId)) return;

            // 閾値を超えた時点でゴーストを最前面の層に作り、以後はカーソルに追従させる
            // Past the threshold, create the ghost on the top layer and follow the cursor
            if (!_dragging && Vector2.Distance(evt.position, _pressPosition) > DragThreshold)
            {
                _dragging = true;
                _tooltips.SetSuppressed(true);
                _ghost.style.backgroundImage = _source.Q(className: "ov-slot__icon").resolvedStyle.backgroundImage;
                _overlay.Add(_ghost);
                _source.AddToClassList("ov-slot--dragging");
            }
            if (!_dragging) return;
            var local = _overlay.WorldToLocal(evt.position);
            _ghost.style.left = local.x - 28;
            _ghost.style.top = local.y - 28;
            SetHovered(SlotAt(evt.position));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_source == null) return;
            this.ReleasePointer(evt.pointerId);
            var target = SlotAt(evt.position);
            if (_dragging && target != null && target != _source)
            {
                // 入れ替え。空きマスへ落とした場合も同じ式で移動になる
                // Swap; dropping on an empty slot becomes a move with the same code
                var (name, count) = (target.ItemName, target.Count);
                target.SetContent(_source.ItemName, _source.Count);
                _source.SetContent(name, count);
            }
            _source.RemoveFromClassList("ov-slot--dragging");
            _ghost.RemoveFromHierarchy();
            _tooltips.SetSuppressed(false);
            SetHovered(null);
            _source = null;
            _dragging = false;
        }

        private void OpenContextMenu(InventorySlotElement slot, Vector2 position)
        {
            // GenericDropdownMenuはランタイムでも使える。右クリック位置に幅0の矩形で出す
            // GenericDropdownMenu works at runtime; open it at the click point with a zero-width rect
            _tooltips.Hide();
            var menu = new GenericDropdownMenu();
            menu.AddItem("半分に分ける", false, () => Split(slot));
            menu.AddItem("1つ捨てる", false, () => slot.SetContent(slot.ItemName, slot.Count - 1));
            menu.AddSeparator("");
            menu.AddDisabledItem("詳細（未実装）", false);
            menu.DropDown(new Rect(position, Vector2.zero), this, DropdownMenuSizeMode.Content);
        }

        private void Split(InventorySlotElement slot)
        {
            var empty = _slots.Find(s => s.ItemName == null);
            if (empty == null || slot.Count < 2)
            {
                Debug.Log("[UitkPoc] 分割できない（空きマスが無いか1個以下）");
                return;
            }
            var half = slot.Count / 2;
            empty.SetContent(slot.ItemName, half);
            slot.SetContent(slot.ItemName, slot.Count - half);
        }

        private InventorySlotElement SlotAt(Vector2 worldPosition)
        {
            // ゴーストは拾わない設定なので、Pickで真下のマスを引ける
            // The ghost ignores picking, so Pick finds the slot underneath
            var picked = panel.Pick(worldPosition);
            while (picked != null && picked is not InventorySlotElement) picked = picked.parent;
            return picked as InventorySlotElement;
        }

        private void SetHovered(InventorySlotElement slot)
        {
            _hovered?.RemoveFromClassList("ov-slot--drop-target");
            _hovered = slot;
            _hovered?.AddToClassList("ov-slot--drop-target");
        }
    }
}
