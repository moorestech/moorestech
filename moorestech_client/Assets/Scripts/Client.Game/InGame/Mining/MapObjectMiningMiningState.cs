using Client.Game.InGame.Player;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Common.Debug;
using Core.Master;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningState : IMapObjectMiningState
    {
        private readonly MiningToolCandidate _miningToolCandidate;

        // 入場時の装備アイテム。毎tickのマスタ走査を避けるため採掘中はこのIDと比較する
        // The item equipped on entry; mining compares against this id to avoid a per-tick master lookup
        private readonly ItemId _startedEquippedItemId;

        private float _currentMiningProgressTime;

        public MapObjectMiningMiningState(MiningToolCandidate miningToolCandidate, ItemId startedEquippedItemId)
        {
            _miningToolCandidate = miningToolCandidate;
            _startedEquippedItemId = startedEquippedItemId;
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
            
            // 左クリックされていない場合はフォーカス状態に遷移
            // If left click is not pressed, transition to focus state
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                return new MapObjectMiningFocusState();
            }

            // 採掘中に対象が採掘対象でなくなったらフォーカス状態でやり直す
            // If the focused object stops being a mining target mid-mining, restart from the focus state
            if (!context.CurrentFocusTarget.IsAvailable || context.CurrentFocusTarget.IsPickUp)
            {
                return new MapObjectMiningFocusState();
            }

            // 採掘はサーバーが装備アイテムでGUID照合するため、装備が変わったら進捗を続けずフォーカスへ戻す
            // The server matches the equipped item's GUID, so an equipment change must drop back to focus instead of advancing
            if (context.LocalPlayerEquipment.SelectedItem.Id != _startedEquippedItemId)
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
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusTarget);
            }
            
            return this;
        }
    }
}
