using Client.Game.InGame.SoundEffect;

namespace Client.Game.InGame.Mining
{
    public class MiningCompleteState : IMiningState
    {
        private readonly IMiningTargetObject _completedTarget;

        public MiningCompleteState(IMiningTargetObject completedTarget)
        {
            _completedTarget = completedTarget;
        }

        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            SoundEffectManager.Instance.PlaySoundEffect(_completedTarget.DestroySoundType);

            // 対象実装へ送信委譲
            // Delegate send to target implementation
            _completedTarget.SendAttack();

            return context.CurrentFocusTarget == null
                ? new MiningIdleState(context)
                : new MiningFocusState();
        }
    }
}
