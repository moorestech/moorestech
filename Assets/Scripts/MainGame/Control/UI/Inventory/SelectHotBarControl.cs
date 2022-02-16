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
        
        public void Awake()
        {
            _inputSettings = new();
            _inputSettings.Enable();
        }

        private void Update()
        {
            //キーボード入力で選択
            if (_inputSettings.UI.HotBar.ReadValue<int>() != 0)
            {
                //キー入力で得られる値は1〜9なので-1する
                _selectIndex = _inputSettings.UI.HotBar.ReadValue<int>() - 1;
            }

            //マウスホイールで選択
            if (_inputSettings.UI.SwitchHotBar.ReadValue<float>() < 0)
            {
                _selectIndex--;
                if (_selectIndex < 0)_selectIndex = HotBarCount - 1;
            }else if (0 < _inputSettings.UI.SwitchHotBar.ReadValue<float>())
            {
                _selectIndex++;
                if (HotBarCount <= _selectIndex)_selectIndex = 0;
            }
            
            //変更を反映する
            selectHotBarView.SetSelect(_selectIndex);
        }
    }
}