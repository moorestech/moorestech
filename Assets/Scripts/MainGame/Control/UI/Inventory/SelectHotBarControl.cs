using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace MainGame.Control.UI.Inventory
{
    public class SelectHotBarControl : MonoBehaviour
    {
        private const int HotBarCount = 9;
        
        private MoorestechInputSettings _inputSettings;
        private int selectIndex;
        
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
                selectIndex = _inputSettings.UI.HotBar.ReadValue<int>();
            }

            //マウスホイールで選択
            if (_inputSettings.UI.SwitchHotBar.ReadValue<float>() < 0)
            {
                selectIndex--;
                if (selectIndex < 0)selectIndex = HotBarCount - 1;
            }else if (0 < _inputSettings.UI.SwitchHotBar.ReadValue<float>())
            {
                selectIndex++;
                if (HotBarCount <= selectIndex)selectIndex = 0;
            }
        }
    }
}