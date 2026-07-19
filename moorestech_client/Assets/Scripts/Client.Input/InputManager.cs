using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Input
{
    public static class InputManager
    {
        private static PayerInputManager player;
        private static PlayableInputManager playable;
        private static UIInputManager ui;
        private static MoorestechInputSettings _instance;
        public static PayerInputManager Player => player ??= new PayerInputManager(Instance);
        
        public static PlayableInputManager Playable => playable ??= new PlayableInputManager(Instance);
        
        public static UIInputManager UI => ui ??= new UIInputManager(Instance);
        
        
        private static MoorestechInputSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MoorestechInputSettings();
                    _instance.Enable();
                }
                
                return _instance;
            }
        }
        
        public static void MouseCursorVisible(bool isVisible)
        {
            Cursor.visible = isVisible;
            Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
    
    public class PayerInputManager
    {
        public readonly InputKey Jump;
        public readonly InputKey Look;
        public readonly InputKey Move;
        public readonly InputKey Sprint;
        
        public PayerInputManager(MoorestechInputSettings settings)
        {
            Move = new InputKey(settings.Player.Move, InputSuppressionScope.Keyboard);
            Look = new InputKey(settings.Player.Look);
            Jump = new InputKey(settings.Player.Jump, InputSuppressionScope.Keyboard);
            Sprint = new InputKey(settings.Player.Sprint, InputSuppressionScope.Keyboard);
        }
    }
    
    public class PlayableInputManager
    {
        public readonly InputKey BlockPlaceRotation;
        public readonly InputKey ClickPosition;
        public readonly InputKey ScreenLeftClick;
        public readonly InputKey ScreenRightClick;
        
        public PlayableInputManager(MoorestechInputSettings settings)
        {
            ScreenLeftClick = new InputKey(settings.Playable.ScreenLeftClick);
            ScreenRightClick = new InputKey(settings.Playable.ScreenRightClick);
            ClickPosition = new InputKey(settings.Playable.ClickPosition);
            BlockPlaceRotation = new InputKey(settings.Playable.BlockPlaceRotation, InputSuppressionScope.Keyboard);
        }
    }
    
    public class UIInputManager
    {
        public readonly InputKey AllCraft;
        public readonly InputKey BlockDelete;
        public readonly InputKey CloseUI;
        public readonly InputKey HotBar;
        public readonly InputKey InventoryItemHalve;
        public readonly InputKey InventoryItemOnePut;
        public readonly InputKey ItemDirectMove;
        public readonly InputKey OneStackCraft;
        public readonly InputKey OpenInventory;
        public readonly InputKey OpenMenu;
        public readonly InputKey QuestUI;
        public readonly InputKey SwitchHotBar;
        
        public UIInputManager(MoorestechInputSettings settings)
        {
            OpenMenu = new InputKey(settings.UI.OpenMenu, InputSuppressionScope.Keyboard);
            CloseUI = new InputKey(settings.UI.CloseUI, InputSuppressionScope.Keyboard);
            OpenInventory = new InputKey(settings.UI.OpenInventory, InputSuppressionScope.Keyboard);
            InventoryItemOnePut = new InputKey(settings.UI.InventoryItemOnePut);
            InventoryItemHalve = new InputKey(settings.UI.InventoryItemHalve);
            HotBar = new InputKey(settings.UI.HotBar, InputSuppressionScope.Keyboard);
            SwitchHotBar = new InputKey(settings.UI.SwitchHotBar);
            BlockDelete = new InputKey(settings.UI.BlockDelete, InputSuppressionScope.Keyboard);
            AllCraft = new InputKey(settings.UI.AllCraft, InputSuppressionScope.Keyboard);
            OneStackCraft = new InputKey(settings.UI.OneStackCraft, InputSuppressionScope.Keyboard);
            QuestUI = new InputKey(settings.UI.QuestUI, InputSuppressionScope.Keyboard);
            ItemDirectMove = new InputKey(settings.UI.ItemDirectMove, InputSuppressionScope.Keyboard);
        }
    }
    
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
            key.started += _ => { if (!IsSuppressed()) OnGetKeyDown?.Invoke(); };
            key.performed += _ => { if (!IsSuppressed()) OnGetKey?.Invoke(); };
            key.canceled += _ => { if (!IsSuppressed()) OnGetKeyUp?.Invoke(); };
        }
        
        public bool GetKeyDown => ReadButton(_inputAction.WasPressedThisFrame());
        public bool GetKey => ReadButton(_inputAction.IsPressed());
        public bool GetKeyUp => ReadButton(_inputAction.WasReleasedThisFrame());
        
        public event Action OnGetKeyDown;
        public event Action OnGetKey;
        public event Action OnGetKeyUp;
        
        public TValue ReadValue<TValue>() where TValue : struct
        {
            var value = _inputAction.ReadValue<TValue>();
            if (IsSuppressed())
            {
                if (!EqualityComparer<TValue>.Default.Equals(value, default)) WebUiInputExclusivity.ProbeSuppressed(_suppressionScope.Value);
                return default;
            }
            return value;
        }

        private bool ReadButton(bool value)
        {
            if (!value || !IsSuppressed()) return value;
            WebUiInputExclusivity.ProbeSuppressed(_suppressionScope.Value);
            return false;
        }

        private bool IsSuppressed()
        {
            return _suppressionScope.HasValue && WebUiInputExclusivity.IsSuppressed(_suppressionScope.Value);
        }
    }
}
