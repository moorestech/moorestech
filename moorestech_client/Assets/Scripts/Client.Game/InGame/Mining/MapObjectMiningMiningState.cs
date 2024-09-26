using Client.Game.InGame.Player;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Mooresmaster.Model.MapObjectsModule;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningState : IMapObjectMiningState
    {
        private readonly MiningToolsElement _miningToolsElement;
        
        private float _currentMiningProgressTime;
        
        public MapObjectMiningMiningState(MiningToolsElement miningToolsElement, IPlayerObjectController playerObjectController)
        {
            _miningToolsElement = miningToolsElement;
            _currentMiningProgressTime = 0;
            
            playerObjectController.SetAnimationState(PlayerAnimationState.Axe);
            ProgressBarView.Instance.Show();
        }
        
        
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            var next = GetNextUpdateInternal(context, dt);
            if (next != this)
            {
                context.PlayerObjectController.SetAnimationState(PlayerAnimationState.IdleWalkRunBlend);
                ProgressBarView.Instance.Hide();
            }
            return next;
        }
        
        private IMapObjectMiningState GetNextUpdateInternal(MapObjectMiningControllerContext context, float dt)
        {
            // フォーカスが外れた場合はidleに遷移
            // if focus is lost, transition to idle
            if (context.CurrentFocusMapObjectGameObject == null)
            {
                return new MapObjectMiningFocusState();
            }
            
            // 左クリックされていない場合はフォーカス状態に遷移
            // If left click is not pressed, transition to focus state
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                return new MapObjectMiningFocusState();
            }
            
            _currentMiningProgressTime += dt;
            ProgressBarView.Instance.SetProgress(_currentMiningProgressTime / _miningToolsElement.AttackSpeed);
            
            // マイニングが完了した場合はマイニング完了状態に遷移
            // If mining is complete, transition to mining complete state
            if (_miningToolsElement.AttackSpeed <= _currentMiningProgressTime)
            {
                var attackDamage = _miningToolsElement.Damage;
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusMapObjectGameObject, attackDamage);
            }
            
            return this;
        }
    }
}