using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Common.Debug;

namespace Client.Game.InGame.Mining
{
    public class MiningProgressState : IMiningState
    {
        private readonly ProgressBarState _progressBar;
        private readonly IMiningTargetObject _startedMiningTarget;
        private readonly MiningToolCandidate _miningToolCandidate;

        private float _currentMiningProgressTime;

        public MiningProgressState(MiningControllerContext context, IMiningTargetObject startedMiningTarget, MiningToolCandidate miningToolCandidate)
        {
            _progressBar = context.ProgressBar;
            _startedMiningTarget = startedMiningTarget;
            _miningToolCandidate = miningToolCandidate;
            _currentMiningProgressTime = 0;

            PlayerSystemContainer.Instance.PlayerObjectController.SetAnimationState(PlayerAnimationState.Axe);
            _progressBar.Show();
        }


        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            var next = GetNextUpdateInternal(context, dt);
            if (next != this)
            {
                PlayerSystemContainer.Instance.PlayerObjectController.SetAnimationState(PlayerAnimationState.IdleWalkRunBlend);
                _progressBar.Hide();
            }
            return next;
        }

        private IMiningState GetNextUpdateInternal(MiningControllerContext context, float dt)
        {
            // フォーカスが外れた場合はidleに遷移
            // if focus is lost, transition to idle
            if (context.CurrentFocusTarget == null)
            {
                return new MiningFocusState();
            }

            // 開始対象が変わればフォーカスへ戻す
            // Return to focus when the started target changes
            if (!ReferenceEquals(context.CurrentFocusTarget, _startedMiningTarget))
            {
                return new MiningFocusState();
            }

            // Fを離したらフォーカス状態に遷移
            // Releasing F returns to the focus state
            if (!InputManager.Playable.Interact.GetKey)
            {
                return new MiningFocusState();
            }

            // 採掘はサーバーが装備アイテムでGUID照合するため、装備が変わったら進捗を続けずフォーカスへ戻す
            // The server matches the equipped item's GUID, so an equipment change must drop back to focus instead of advancing
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            if (equippedItemId != _miningToolCandidate.ToolItemId)
            {
                return new MiningFocusState();
            }

            // 採掘中に対象が進捗採掘の条件を失ったらフォーカス状態でやり直す
            // If the target stops satisfying progress mining mid-swing, restart from the focus state
            if (_startedMiningTarget.TryBeginHandMining(equippedItemId, out _) != MiningStartOutcome.Ready)
            {
                return new MiningFocusState();
            }

            // 高速採掘デバッグはmapObject専用のため、veinの露頭には適用しない
            // The super-mine debug is map-object only, so it must not accelerate vein outcrops
            if (_startedMiningTarget is MapObjectGameObject &&
                DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.MapObjectSuperMine))
            {
                dt *= 1000;
            }

            _currentMiningProgressTime += dt;
            _progressBar.SetProgress(_currentMiningProgressTime / _miningToolCandidate.AttackSpeed);

            // マイニングが完了した場合はマイニング完了状態に遷移
            // If mining is complete, transition to mining complete state
            if (_miningToolCandidate.AttackSpeed <= _currentMiningProgressTime)
            {
                return new MiningCompleteState(_startedMiningTarget);
            }

            return this;
        }
    }
}
