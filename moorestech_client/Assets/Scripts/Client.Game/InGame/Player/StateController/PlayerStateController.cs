using System;
using Client.Game.InGame.UI.UIState;
using VContainer.Unity;

namespace Client.Game.InGame.Player.StateController
{
    // UIStateControl の変化を受け取り、プレイヤー側のステート（Normal / Riding）を駆動するコントローラ。
    // 依存方向は常に UIState → PlayerStateController（PlayerStateController から UIState への能動呼び出しは禁止）。
    // Drives player-side state (Normal / Riding) in response to UIStateControl changes.
    // Dependency always flows UIState → PlayerStateController (never the reverse).
    public class PlayerStateController : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerStateDictionary _stateDictionary;
        private readonly UIStateControl _uiStateControl;

        public PlayerStateEnum CurrentState { get; private set; } = PlayerStateEnum.Normal;
        public event Action<PlayerStateEnum> OnStateChanged;

        public PlayerStateController(PlayerStateDictionary stateDictionary, UIStateControl uiStateControl)
        {
            _stateDictionary = stateDictionary;
            _uiStateControl = uiStateControl;
        }

        public void Initialize()
        {
            // UIStateControl.Start() で初期 OnEnter が呼ばれた後にイベントを購読する。
            // Subscribe after UIStateControl.Start() has fired its initial OnEnter.
            _uiStateControl.OnStateChanged += HandleUIStateChanged;
            _stateDictionary.GetState(CurrentState).OnEnter(new PlayerTransitContext(CurrentState));
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= HandleUIStateChanged;
        }

        public void Tick()
        {
            _stateDictionary.GetState(CurrentState).Tick();
        }

        private void HandleUIStateChanged(UIStateEnum uiState)
        {
            var nextState = MapUIStateToPlayerState(uiState);
            if (nextState == CurrentState) return;

            var lastState = CurrentState;
            CurrentState = nextState;

            _stateDictionary.GetState(lastState).OnExit();
            _stateDictionary.GetState(nextState).OnEnter(new PlayerTransitContext(lastState));

            OnStateChanged?.Invoke(nextState);
        }

        // 現状は TrainHUDScreen のみ Riding に対応する。インベントリ等から戻った直後も
        // UIState=GameScreen なので Normal となる。乗車中インベントリは将来課題。
        // Currently only TrainHUDScreen maps to Riding. After returning from inventory etc.,
        // UIState=GameScreen so player state becomes Normal. Inventory-while-riding is future work.
        private static PlayerStateEnum MapUIStateToPlayerState(UIStateEnum uiState)
        {
            return uiState == UIStateEnum.TrainHUDScreen ? PlayerStateEnum.Riding : PlayerStateEnum.Normal;
        }
    }
}
