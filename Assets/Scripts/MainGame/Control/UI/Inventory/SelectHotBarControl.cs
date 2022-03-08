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
        
        private const int HotBarCount = 9;
        
        private MoorestechInputSettings _inputSettings;
        private int _selectIndex = 0;

        public int SelectIndex => _selectIndex;

        public void Awake()
        {
            _inputSettings = new();
            _inputSettings.Enable();
        }
        
    }
}