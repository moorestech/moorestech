using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Client.Input
{
    public class InputKey
    {
        private readonly InputAction _inputAction;
        private readonly InputSuppressionScope? _suppressionScope;

        public InputKey(InputAction key) : this(key, null)
        {
        }

        public InputKey(InputAction key, InputSuppressionScope suppressionScope) : this(key, (InputSuppressionScope?)suppressionScope)
        {
        }

        private InputKey(InputAction key, InputSuppressionScope? suppressionScope)
        {
            _inputAction = key;
            _suppressionScope = suppressionScope;
            key.started += _ => { if (FilterPress(true)) OnGetKeyDown?.Invoke(); };
            key.performed += _ => { if (FilterPress(true)) OnGetKey?.Invoke(); };
            // 解放待ちラッチへ、入力抑止中も実解放を届ける
            // Deliver releases during suppression so release-wait latches can reset
            key.canceled += _ => OnGetKeyUp?.Invoke();
        }
        
        public bool GetKeyDown => FilterPress(_inputAction.WasPressedThisFrame() || TestKeyDown);
        public bool GetKey => FilterPress(_inputAction.IsPressed());
        public bool GetKeyUp => InputSuppressionFilter.FilterKeyRelease(_inputAction.WasReleasedThisFrame(), _suppressionScope);
        
        // このキーに割り当てた実コントロールか（修飾キー判定で自分自身を除くため）
        // Whether the control is bound to this key (to exclude itself when detecting modifier use)
        public bool IsBoundControl(InputControl control)
        {
            return _inputAction.controls.Contains(control);
        }

        public event Action OnGetKeyDown;
        public event Action OnGetKey;
        public event Action OnGetKeyUp;
        
        public TValue ReadValue<TValue>() where TValue : struct
        {
            var value = _inputAction.ReadValue<TValue>();
            var hasNonDefaultValue = !EqualityComparer<TValue>.Default.Equals(value, default);
            return InputSuppressionFilter.FilterValueInput(hasNonDefaultValue, _suppressionScope) ? value : default;
        }

        // 再バインドに追従するため判定のたびに自分のコントロールにESCが含まれるかを見る
        // Checked on every read so rebinding is followed: does this action's control set contain ESC
        private bool FilterPress(bool pressed)
        {
            if (!pressed) return false;
            var keyboard = Keyboard.current;
            var isEscapeKey = keyboard != null && IsBoundControl(keyboard.escapeKey);
            return InputSuppressionFilter.FilterKeyPress(pressed, _suppressionScope, isEscapeKey);
        }

#if UNITY_EDITOR
        // EditModeテストではInputSystemの押下がWasPressedThisFrameへ届かないため、押下だけをテストから差し込む
        // In EditMode tests an Input System press never reaches WasPressedThisFrame, so tests inject the press itself
        private bool TestKeyDown { get; set; }

        internal void SetKeyDownForTest(bool isKeyDown)
        {
            TestKeyDown = isKeyDown;
        }
#else
        private bool TestKeyDown => false;
#endif
    }
}
