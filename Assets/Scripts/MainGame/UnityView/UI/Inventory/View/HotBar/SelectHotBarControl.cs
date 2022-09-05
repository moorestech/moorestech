using System;
using MainGame.UnityView.UI.Builder;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class SelectHotBarControl : MonoBehaviour
    {
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private HotBarItemView hotBarItemView;
        
        private MoorestechInputSettings _inputSettings;
        
        public event Action<int> OnSelectHotBar;

        public int SelectIndex => _selectIndex;
        private int _selectIndex = 0;
        

        public bool IsClicked => _isClickedCount == 0 || _isClickedCount == 1;
        private int _isClickedCount = -1;
        
        public void Start()
        {
            _inputSettings = new();
            _inputSettings.Enable();

            foreach (var slot in hotBarItemView.Slots)
            {
                slot.OnLeftClickDown += ClickItem;
            }
            
        }

        private void ClickItem(UIBuilderItemSlotObject uiBuilderItemSlotObject)
        {
            var slot = 0;
            for (var i = 0; i < hotBarItemView.Slots.Count; i++)
            {
                if (uiBuilderItemSlotObject == hotBarItemView.Slots[i])
                {
                    slot = i;
                }
            }
            _selectIndex = slot;
            _isClickedCount = 0;
            selectHotBarView.SetSelect(slot);
            
            OnSelectHotBar?.Invoke(_selectIndex);
        }

        private void Update()
        {
            //キーボード入力で選択
            if (_inputSettings.UI.HotBar.ReadValue<int>() != 0)
            {
                //キー入力で得られる値は1〜9なので-1する
                _selectIndex = _inputSettings.UI.HotBar.ReadValue<int>() - 1;
                selectHotBarView.SetSelect(_selectIndex);
                
                OnSelectHotBar?.Invoke(_selectIndex);
            }
            
            // ButtonがクリックされたことをUpdate内で確認したいのでクリックされてから2フレームはtrueとする
            if (_isClickedCount == 0 || _isClickedCount == 1)
            {
                _isClickedCount++;
            }
            if (_isClickedCount == 2)
            {
                _isClickedCount = -1;
            }
        }
    }
}