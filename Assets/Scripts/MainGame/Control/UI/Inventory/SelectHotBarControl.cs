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
        
        private const int HotBarCount = 9;
        
        private MoorestechInputSettings _inputSettings;
        private int _selectIndex = 0;

        public int SelectIndex => _selectIndex;


        public bool IsClicked => isClicked;
        private bool isClicked = false;
        
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
            selectHotBarView.SetSelect(slot);
        }

        public void LateUpdate()
        {
            isClicked = false;
        }
    }
}