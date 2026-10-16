using System;
using Client.Input;
using UniRx;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine;

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		public bool inputEnable { get; private set; } = true;
		
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
        private SprintInputResolver _sprintResolver;
        private IDisposable _modeSubscription;
        private IDisposable _buttonSubscription;

        private void Awake()
        {
            // 現在の方式と設定変更を入力へ反映
            // Apply the current mode and setting changes to input
            _sprintResolver = new SprintInputResolver(SprintInputModePreference.Current);
            _modeSubscription = SprintInputModePreference.OnSprintInputModeChanged.Subscribe(mode =>
            {
                _sprintResolver.SetMode(mode);
                ApplySprint();
            });
            ApplySprint();
        }

        private void OnEnable()
        {
            // 未観測期間をまたぐ押下候補は破棄する
            // Discard tap candidates spanning an unobserved interval
            _sprintResolver.ResyncHeld(inputEnable && InputManager.Player.Sprint.GetKey);
            ApplySprint();

            // 走り以外の押下は修飾操作とする
            // Treat non-sprint presses as modifier operations
            _buttonSubscription = InputSystem.onAnyButtonPress.Call(control =>
            {
                // silent-ok: Sprint自身は修飾操作ではない / 入力テストで確認する
                if (InputManager.Player.Sprint.IsBoundControl(control)) return;
                _sprintResolver.OnOtherButtonPressed();
            });
        }

        private void OnDisable() => _buttonSubscription.Dispose();
        private void OnDestroy() => _modeSubscription.Dispose();

        public void SetSprintToggleAllowed(bool allowed) => _sprintResolver.SetToggleAllowed(allowed);

        private void ApplySprint() => sprint = inputEnable && _sprintResolver.IsSprinting;

		[Header("Movement Settings")]
		public bool analogMovement;

#if !UNITY_IOS || !UNITY_ANDROID
		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;
#endif

		public void SetInputEnable(bool enable)
		{
			// 同値の再適用で押下中のジャンプ等を捨てないよう、切替時だけ処理する
			// Act only on an actual toggle so a same-value reapply never drops an in-flight jump
			if (inputEnable == enable) return;
			inputEnable = enable;

			// 無効中は押下も離しも捨てられるため、切替の瞬間に押しっぱなしの実値へ揃え直す
			// Presses and releases are dropped while disabled, so resync to the keys actually held at the moment of toggling
			jump = false;
			look = Vector2.zero;
			move = enable ? InputManager.Player.Move.ReadValue<Vector2>() : Vector2.zero;
			_sprintResolver.ResyncHeld(enable && InputManager.Player.Sprint.GetKey);
            ApplySprint();
		}

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
		public void OnMove(InputValue value)
		{
			if (!inputEnable) return; 
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (!inputEnable) return; 
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			if (!inputEnable) return; 
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			if (!inputEnable) return; 
			SprintInput(value.isPressed);
		}
#else
	// old input sys if we do decide to have it (most likely wont)...
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
            if (!inputEnable) return; 
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
            if (!inputEnable) return;
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
            if (!inputEnable) return;
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
            if (!inputEnable) return;
			// 押下列の解決は入力層に集める
            // Keep press-sequence resolution in the input layer
            if (newSprintState) _sprintResolver.OnSprintPressed();
            else _sprintResolver.OnSprintReleased();
            ApplySprint();
		}

#if !UNITY_IOS || !UNITY_ANDROID

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

#endif

	}
	
}