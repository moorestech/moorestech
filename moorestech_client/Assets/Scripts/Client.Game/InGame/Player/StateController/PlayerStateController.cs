using VContainer.Unity;

namespace Client.Game.InGame.Player.StateController
{
    // プレイヤー側のステート（Normal / Riding）を保持・遷移するコントローラ。
    // 自発的な遷移判定は持たず、必ず外部からの SetState 呼び出しで状態が変化する。
    // 依存方向は 外部 (例: TrainHUDScreenState) → PlayerStateController の一本道。
    // Holds and transitions player-side state (Normal / Riding).
    // Never decides transitions itself: external callers (e.g. TrainHUDScreenState) drive changes via SetState.
    // Dependency flows one-way external → PlayerStateController.
    public class PlayerStateController : ITickable
    {
        private readonly PlayerStateDictionary _stateDictionary;

        public PlayerStateEnum CurrentState { get; private set; } = PlayerStateEnum.Normal;

        public PlayerStateController(PlayerStateDictionary stateDictionary)
        {
            _stateDictionary = stateDictionary;
        }

        public void Tick()
        {
            _stateDictionary.GetState(CurrentState).Tick();
        }

        // 外部から呼ばれる唯一の状態変更入口。
        // OnExit → CurrentState 更新 → OnEnter の順で実行し、OnExit 例外時の中間状態を防ぐ。
        // context は遷移先の State が必要な情報を保持する（例: Riding なら IPlayerRideContext）。null 可。
        // The only entry point that mutates the current state.
        // Order is OnExit → CurrentState update → OnEnter so an exception in OnExit doesn't leave CurrentState half-updated.
        // context carries information the target State needs (e.g. IPlayerRideContext for Riding); nullable.
        public void SetState(PlayerStateEnum nextState, IPlayerStateContext context)
        {
            if (nextState == CurrentState) return;

            var lastState = CurrentState;
            _stateDictionary.GetState(lastState).OnExit();
            CurrentState = nextState;
            _stateDictionary.GetState(nextState).OnEnter(context);
        }
    }
}
