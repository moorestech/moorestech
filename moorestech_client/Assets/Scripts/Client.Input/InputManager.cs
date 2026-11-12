using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Client.Input
{
    public static class InputManager
    {
        private static PayerInputManager player;
        private static PlayableInputManager playable;
        private static UIInputManager ui;
        private static MoorestechInputSettings _instance;
        private static readonly Subject<bool> CursorLockChangedSubject = new();

        // カーソルロックが変わった時に、新しいロック状態（true=ロック）を書き換えた後で流す
        // Publishes the new lock state (true = locked) after writing it, whenever the cursor lock changes
        public static IObservable<bool> OnCursorLockChanged => CursorLockChangedSubject;

        // ロック状態の正本。唯一の書き手 MouseCursorVisible が書いた値を持つ（batchmode は Cursor.lockState への代入を捨て None のまま返すため、Cursor から読み戻さない）
        // Source of truth for the lock; holds what the only writer MouseCursorVisible wrote (batchmode drops writes to Cursor.lockState and keeps returning None, so it is never read back from Cursor)
        public static bool IsCursorLocked { get; private set; }
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
            // ロック中はカーソル座標が凍結するため、どの経路からのロックでも直前に中央へ寄せてクロスヘアと一致させる
            // The cursor freezes while locked, so every lock path centers it first to match the crosshair
            if (!isVisible) WarpMouseCursorToScreenCenter();

            var wasLocked = IsCursorLocked;
            var isLocked = !isVisible;
            Cursor.visible = isVisible;
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            IsCursorLocked = isLocked;

            // 同じ状態の再指定では流さない
            // Re-applying the same state does not publish
            if (wasLocked != isLocked) CursorLockChangedSubject.OnNext(isLocked);
        }

        public static void WarpMouseCursorToScreenCenter()
        {
            // ロック解除直後のカーソル出現位置はOS任せのため明示的に中央へ寄せる
            // The cursor's spawn position right after unlock is OS-dependent, so warp it to center explicitly
            if (Mouse.current == null) return;
            var screenCenter = ScreenCenter.GetPosition();
            Mouse.current.WarpCursorPosition(screenCenter);

            // WarpCursorPositionのpositionは次の入力更新まで古いままなので同フレーム参照用に直接書く
            // WarpCursorPosition leaves position stale until the next input update, so write it for same-frame reads
            InputState.Change(Mouse.current.position, screenCenter);
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
        public readonly InputKey Interact;
        public readonly InputKey Ride;

        public PlayableInputManager(MoorestechInputSettings settings)
        {
            ScreenLeftClick = new InputKey(settings.Playable.ScreenLeftClick);
            ClickPosition = new InputKey(settings.Playable.ClickPosition);
            BlockPlaceRotation = new InputKey(settings.Playable.BlockPlaceRotation, InputSuppressionScope.Keyboard);

            // Web UIのテキスト入力中に世界へ漏れないようキーボード抑止スコープに入れる
            // Keep both under the keyboard suppression scope so Web UI text input never leaks into the world
            Interact = new InputKey(settings.Playable.Interact, InputSuppressionScope.Keyboard);
            Ride = new InputKey(settings.Playable.Ride, InputSuppressionScope.Keyboard);
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
        public readonly InputKey SkitAdvance;
        
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

            // スキットの送りの Enter。入力欄の Enter と取り合わないようキーボード抑止スコープに入れる
            // Skit-advance Enter; keep it under the keyboard suppression scope so it never competes with text-field Enter
            SkitAdvance = new InputKey(settings.UI.SkitAdvance, InputSuppressionScope.Keyboard);
        }
    }
}
