using VContainer.Unity;

namespace Client.Game.InGame.Player.StateController
{
    // プレイヤー側のステート（Normal / Riding）を保持・遷移するコントローラ。
    // 自発的な遷移判定は持たず、必ず外部（UIState 側）からの SetState 呼び出しで状態が変化する。
    // 依存方向は UIStateControl → PlayerStateController の一本道で、逆方向の参照は行わない。
    // Holds and transitions player-side state (Normal / Riding).
    // Never decides transitions itself: external callers (UIState side) drive changes via SetState.
    // Dependency flows one-way UIStateControl → PlayerStateController; no reverse reference.
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

        // 外部（UIStateControl など）から呼ばれる唯一の状態変更入口。
        // OnExit → CurrentState 更新 → OnEnter の順で実行する（OnExit 例外時の中間状態を防ぐ）。
        // The only entry point that mutates the current state; called by UIStateControl etc.
        // Order is OnExit → CurrentState update → OnEnter so that an exception in OnExit doesn't leave CurrentState half-updated.
        public void SetState(PlayerStateEnum nextState)
        {
            if (nextState == CurrentState) return;

            var lastState = CurrentState;
            _stateDictionary.GetState(lastState).OnExit();
            CurrentState = nextState;
            _stateDictionary.GetState(nextState).OnEnter();
        }
    }
}
