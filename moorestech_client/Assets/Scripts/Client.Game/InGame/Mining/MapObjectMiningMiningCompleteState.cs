using Client.Game.InGame.SoundEffect;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningCompleteState : IMapObjectMiningState
    {
        private readonly IMiningTargetObject _completedTarget;

        public MapObjectMiningMiningCompleteState(IMiningTargetObject completedTarget)
        {
            _completedTarget = completedTarget;
        }

        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            SoundEffectManager.Instance.PlaySoundEffect(_completedTarget.DestroySoundType);

            // 対象固有のサーバー攻撃送信は実装側へ委譲する
            // Delegate target-specific server attack sending to the implementation
            _completedTarget.SendAttack();

            return context.CurrentFocusTarget == null
                ? new MapObjectMiningIdleState()
                : new MapObjectMiningFocusState();
        }
    }
}
