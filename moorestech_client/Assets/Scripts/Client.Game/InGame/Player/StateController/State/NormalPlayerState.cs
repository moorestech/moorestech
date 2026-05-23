namespace Client.Game.InGame.Player.StateController.State
{
    // 通常時の Player ステート。徒歩操作は ThirdPersonController が常時担当しているため、
    // ここでは特別な処理を持たない（降車時の pose 復帰は RidingPlayerState.OnExit で行う）。
    // Normal-time player state. ThirdPersonController handles walking, so this state does nothing
    // (the dismount pose restoration runs inside RidingPlayerState.OnExit).
    public class NormalPlayerState : IPlayerState
    {
        public void OnEnter()
        {
        }

        public void Tick()
        {
        }

        public void OnExit()
        {
        }
    }
}
