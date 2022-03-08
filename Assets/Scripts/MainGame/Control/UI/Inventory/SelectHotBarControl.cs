using System;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace MainGame.Control.UI.Inventory
{
    public class SelectHotBarControl : MonoBehaviour
    {
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private HotBarItemView hotBarItemView;
        
        
        private MoorestechInputSettings _inputSettings;
        
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
                slot.SubscribeOnItemSlotClick(ClickItem);
            }
        }

        private void ClickItem(int slot)
        {
            _selectIndex = slot;
            _isClickedCount = 0;
            Debug.Log("Clicked");
            selectHotBarView.SetSelect(slot);
        }

        /// <summary>
        /// ButtonがクリックされたことをFixedUpdate内で確認したいのでクリックされてから2フレームはtrueとする
        /// </summary>
        public void FixedUpdate()
        {
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