using Client.Game.InGame.Player;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Common.Debug;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningState : IMapObjectMiningState
    {
        private readonly IMiningTargetObject _startedMiningTarget;
        private readonly MiningToolCandidate _miningToolCandidate;

        private float _currentMiningProgressTime;

        public MapObjectMiningMiningState(IMiningTargetObject startedMiningTarget, MiningToolCandidate miningToolCandidate)
        {
            _startedMiningTarget = startedMiningTarget;
            _miningToolCandidate = miningToolCandidate;
            _currentMiningProgressTime = 0;
            
            PlayerSystemContainer.Instance.PlayerObjectController.SetAnimationState(PlayerAnimationState.Axe);
            ProgressBarView.Instance.Show();
        }
        
        
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            var next = GetNextUpdateInternal(context, dt);
            if (next != this)
            {
                PlayerSystemContainer.Instance.PlayerObjectController.SetAnimationState(PlayerAnimationState.IdleWalkRunBlend);
                ProgressBarView.Instance.Hide();
            }
            return next;
        }
        
        private IMapObjectMiningState GetNextUpdateInternal(MapObjectMiningControllerContext context, float dt)
        {
            // フォーカスが外れた場合はidleに遷移
            // if focus is lost, transition to idle
            if (context.CurrentFocusTarget == null)
            {
                return new MapObjectMiningFocusState();
            }

            // 開始対象が変わればフォーカスへ戻す
            // Return to focus when the started target changes
            if (!ReferenceEquals(context.CurrentFocusTarget, _startedMiningTarget))
            {
                return new MapObjectMiningFocusState();
            }
            
            // 左クリックされていない場合はフォーカス状態に遷移
            // If left click is not pressed, transition to focus state
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                return new MapObjectMiningFocusState();
            }

            // 採掘中に対象が採掘対象でなくなったらフォーカス状態でやり直す
            // If the focused object stops being a mining target mid-mining, restart from the focus state
            if (!_startedMiningTarget.IsAvailable || _startedMiningTarget.IsPickUp)
            {
                return new MapObjectMiningFocusState();
            }

            // 採掘はサーバーが装備アイテムでGUID照合するため、装備が変わったら進捗を続けずフォーカスへ戻す
            // The server matches the equipped item's GUID, so an equipment change must drop back to focus instead of advancing
            if (context.LocalPlayerEquipment.SelectedItem.Id != _miningToolCandidate.ToolItemId)
            {
                return new MapObjectMiningFocusState();
            }

            // デバッグ用で高速マイニングする
            // For debugging, mine super fast
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.MapObjectSuperMine))
            {
                dt *= 1000;
            }
            
            _currentMiningProgressTime += dt;
            ProgressBarView.Instance.SetProgress(_currentMiningProgressTime / _miningToolCandidate.AttackSpeed);
            
            // マイニングが完了した場合はマイニング完了状態に遷移
            // If mining is complete, transition to mining complete state
            if (_miningToolCandidate.AttackSpeed <= _currentMiningProgressTime)
            {
                return new MapObjectMiningMiningCompleteState(_startedMiningTarget);
            }
            
            return this;
        }
    }
}
