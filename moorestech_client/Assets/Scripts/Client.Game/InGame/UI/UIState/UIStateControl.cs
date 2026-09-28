using System;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        private UIStateDictionary _uiStateDictionary;
        private IPlayerObjectController _playerObjectController;

        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; }

        private UIStateEnum? _webTransitionRequest;
        private IDisposable _subStateMovementLockSubscription;

        [Inject]
        public void Construct(UIStateDictionary uiStateDictionary, IPlayerObjectController playerObjectController)
        {
            _uiStateDictionary = uiStateDictionary;
            _playerObjectController = playerObjectController;
        }

        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            EnterState(CurrentState, initialContext);
        }

        // 現stateが入れ子ポーズを持つ画面かの解決口。Web境界はこの1箇所だけを見る（ADR 0035）
        // Single resolution point for whether the current state owns a nested pause; the web boundary looks only here (ADR 0035)
        public INestedPauseScreenState GetCurrentNestedPauseScreen()
        {
            return _uiStateDictionary.GetState(CurrentState) as INestedPauseScreenState;
        }
        
        // Web UI からの遷移要求を受け付ける（次のUpdateで最優先消費）
        // Accept a transition request from the Web UI (consumed first in the next Update)
        public void RequestTransition(UIStateEnum nextState)
        {
            _webTransitionRequest = nextState;
        }

        // UI state
        private void Update()
        {
            // Web要求を最優先で消費し、無ければ現stateの入力判定を使う
            // Consume the web request first; otherwise poll the current state's input
            var nextContext = ConsumeWebRequest() ?? _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            // 終了処理中のサブステート遷移で前画面の宣言を再適用しないよう、先に購読を切る
            // Unsubscribe first so a sub-state transition during teardown cannot re-apply the leaving screen's declaration
            DisposeSubStateMovementLockSubscription();
            _uiStateDictionary.GetState(lastState).OnExit();
            CurrentState = nextContext.NextStateEnum;
            EnterState(CurrentState, nextContext);

            OnStateChanged?.Invoke(CurrentState);

            #region Internal

            UITransitContext ConsumeWebRequest()
            {
                if (_webTransitionRequest == null) return null;
                var requested = _webTransitionRequest.Value;
                _webTransitionRequest = null;

                // 同一stateへの要求は遷移不要
                // A request for the current state needs no transition
                if (requested == CurrentState) return null;
                return new UITransitContext(requested);
            }

            #endregion
        }

        private void EnterState(UIStateEnum state, UITransitContext context)
        {
            // 前画面の入れ子購読は持ち越さない
            // Never carry the previous screen's nested subscription over
            DisposeSubStateMovementLockSubscription();

            // 移動可否は画面自身の宣言に従い、OnEnterより先に確定させる
            // Movement follows the screen's own declaration and is settled before OnEnter runs
            var uiState = _uiStateDictionary.GetState(state);
            ApplyMovementLock(uiState);

            // 入れ子ポーズの出入りでも宣言が変わるため、サブステート遷移を購読して再適用する
            // The declaration also changes when a nested pause opens or closes, so re-apply on each sub-state transition
            if (uiState is INestedPauseScreenState nestedPauseScreen)
                _subStateMovementLockSubscription = nestedPauseScreen.OnSubStateChanged.Subscribe(_ => ApplyMovementLock(uiState));

            uiState.OnEnter(context);
        }

        private void ApplyMovementLock(IUIState uiState)
        {
            _playerObjectController.SetMovementLock(PlayerMovementLockReason.Ui, uiState.LocksPlayerMovement());
        }

        private void DisposeSubStateMovementLockSubscription()
        {
            _subStateMovementLockSubscription?.Dispose();
            _subStateMovementLockSubscription = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;

            // フォーカスイベントは初期化(Initialize)前にも飛んでくるため未初期化中は無視する（ライフサイクル境界）
            // Focus events can arrive before Initialize, so ignore them while the dictionary is not built yet (lifecycle boundary)
            if (_uiStateDictionary == null) return;

            if (_uiStateDictionary.GetState(CurrentState) is IApplicationFocusRestorer focusRestorer)
                focusRestorer.RestoreAfterApplicationFocus();
        }
    }
}
