using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.Element;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.HotBar
{
    public class SelectHotBarControl : MonoBehaviour
    {
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private HotBarItemView hotBarItemView;

        public int SelectIndex { get; private set; }

        public event Action<int> OnSelectHotBar;

        public void Start()
        {
            foreach (var slot in hotBarItemView.Slots)
                slot.OnLeftClickDown.Subscribe(ClickItem);
        }

        private void Update()
        {
            //キーボード入力で選択
            if (InputManager.UI.HotBar.ReadValue<int>() == 0) return;
            
            //キー入力で得られる値は1〜9なので-1する
            SelectIndex = InputManager.UI.HotBar.ReadValue<int>() - 1;
            selectHotBarView.SetSelect(SelectIndex);

            OnSelectHotBar?.Invoke(SelectIndex);
        }

        private void ClickItem(ItemSlotObject itemSlotObject)
        {
            var slot = 0;
            for (var i = 0; i < hotBarItemView.Slots.Count; i++)
                if (itemSlotObject == hotBarItemView.Slots[i])
                    slot = i;
            SelectIndex = slot;
            selectHotBarView.SetSelect(slot);

            OnSelectHotBar?.Invoke(SelectIndex);
        }
    }
}