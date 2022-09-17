using System;
using UnityEngine.InputSystem;

namespace MainGame.UnityView.Control
{
    public class InputManager
    {
        public static readonly MoorestechInputSettings Settings = new();
    }

    public class InputKey
    {
        private readonly InputAction _inputAction;
        public event Action OnGetKeyDown;
        public event Action OnGetKey;
        public event Action OnGetKeyUp;
        
        public bool GetKeyDown => _inputAction.WasPressedThisFrame();
        public bool GetKey => _inputAction.IsPressed();
        public bool GetKeyUp => _inputAction.WasReleasedThisFrame();
        
        
        public InputKey(InputAction key)
        {
            key.started += _ => { OnGetKeyDown?.Invoke(); };
            key.performed += _ => { OnGetKey?.Invoke(); };
            key.canceled += _ => { OnGetKeyUp?.Invoke(); };
        }
    }
}