using Client.Game.InGame.Player;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Common.Debug;
using Mooresmaster.Model.MapModule;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningState : IMapObjectMiningState
    {
        private readonly MiningToolsElement _miningToolsElement;
        
        private float _currentMiningProgressTime;
        
        public MapObjectMiningMiningState(MiningToolsElement miningToolsElement)
        {
            _miningToolsElement = miningToolsElement;
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

            // 採掘中に対象が採掘対象でなくなったらフォーカス状態でやり直す
            // If the focused object stops being a mining target mid-mining, restart from the focus state
            var masterElement = context.CurrentFocusMapObjectGameObject.MapObjectMasterElement;
            if (masterElement == null || masterElement.MiningType != MapObjectMasterElement.MiningTypeConst.Mining)
            {
                return new MapObjectMiningFocusState();
            }

            // 採掘はサーバーが装備アイテムでGUID照合するため、装備が変わったら進捗を続けずフォーカスへ戻す
            // The server matches the equipped item's GUID, so an equipment change must drop back to focus instead of advancing
            var miningTools = ((MiningMiningParam)masterElement.MiningParam).MiningTools;
            if (MapObjectMiningFocusState.ResolveUsableTool(miningTools, context.LocalPlayerEquipment.SelectedItem.Id) != _miningToolsElement)
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
            ProgressBarView.Instance.SetProgress(_currentMiningProgressTime / _miningToolsElement.AttackSpeed);
            
            // マイニングが完了した場合はマイニング完了状態に遷移
            // If mining is complete, transition to mining complete state
            if (_miningToolsElement.AttackSpeed <= _currentMiningProgressTime)
            {
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusMapObjectGameObject);
            }
            
            return this;
        }
    }
}
