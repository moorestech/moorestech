using System;
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
            Move = new InputKey(settings.Player.Move);
            Look = new InputKey(settings.Player.Look);
            Jump = new InputKey(settings.Player.Jump);
            Sprint = new InputKey(settings.Player.Sprint);
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
            BlockPlaceRotation = new InputKey(settings.Playable.BlockPlaceRotation);
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
        public readonly InputKey ResearchTree;
        public readonly InputKey SwitchHotBar;
        
        public UIInputManager(MoorestechInputSettings settings)
        {
            OpenMenu = new InputKey(settings.UI.OpenMenu);
            CloseUI = new InputKey(settings.UI.CloseUI);
            OpenInventory = new InputKey(settings.UI.OpenInventory);
            InventoryItemOnePut = new InputKey(settings.UI.InventoryItemOnePut);
            InventoryItemHalve = new InputKey(settings.UI.InventoryItemHalve);
            HotBar = new InputKey(settings.UI.HotBar);
            SwitchHotBar = new InputKey(settings.UI.SwitchHotBar);
            BlockDelete = new InputKey(settings.UI.BlockDelete);
            AllCraft = new InputKey(settings.UI.AllCraft);
            OneStackCraft = new InputKey(settings.UI.OneStackCraft);
            ResearchTree = new InputKey(settings.UI.ResearchTree);
            ItemDirectMove = new InputKey(settings.UI.ItemDirectMove);
        }
    }
    
    public class InputKey
    {
        private readonly InputAction _inputAction;
        
        
        public InputKey(InputAction key)
        {
            _inputAction = key;
            key.started += _ => { OnGetKeyDown?.Invoke(); };
            key.performed += _ => { OnGetKey?.Invoke(); };
            key.canceled += _ => { OnGetKeyUp?.Invoke(); };
        }
        
        public bool GetKeyDown => _inputAction.WasPressedThisFrame();
        public bool GetKey => _inputAction.IsPressed();
        public bool GetKeyUp => _inputAction.WasReleasedThisFrame();
        
        public event Action OnGetKeyDown;
        public event Action OnGetKey;
        public event Action OnGetKeyUp;
        
        public TValue ReadValue<TValue>() where TValue : struct
        {
            return _inputAction.ReadValue<TValue>();
        }
    }
}
