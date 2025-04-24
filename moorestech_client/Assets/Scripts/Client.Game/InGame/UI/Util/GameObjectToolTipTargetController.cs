using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.Util
{
    /// <summary>
    ///     GameObjectのマウスカーソル説明コンポーネントにマウスカーソルが乗っているかを統合的に管理するシステム
    /// </summary>
    public class GameObjectToolTipTargetController : MonoBehaviour
    {
        private GameObjectTooltipTarget _lastTooltipTarget;
        
        private void Update()
        {
            if (TryGetOnCursorTooltipTarget(out var target))
            {
                if (_lastTooltipTarget == target) return;
                
                if (_lastTooltipTarget != null) _lastTooltipTarget.OnCursorExit();
                target.OnCursorEnter();
                _lastTooltipTarget = target;
            }
            else
            {
                if (_lastTooltipTarget != null) _lastTooltipTarget.OnCursorExit();
                _lastTooltipTarget = null;
            }
        }
        
        private bool TryGetOnCursorTooltipTarget(out GameObjectTooltipTarget target)
        {
            target = null;
            var meinCamera = Camera.main;
            if (meinCamera == null) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = meinCamera.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100)) return false;
            
            if (!hit.collider.gameObject.TryGetComponent<GameObjectTooltipTarget>(out var enterTarget)) return false;
            
            target = enterTarget;
            return true;
        }
    }
}