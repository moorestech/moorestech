using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     GameObjectのマウスカーソル説明コンポーネントにマウスカーソルが乗っているかを統合的に管理するシステム
    /// </summary>
    public class GameObjectToolTipTargetController : MonoBehaviour
    {
        [Inject] private IMouseCursorTooltip _tooltip;

        private GameObjectTooltipTarget _lastTooltipTarget;

        private void Update()
        {
            // コンテナ構築完了（StartGame）前は_tooltipが未注入のためガードする
            // Guards against the pre-DI window before StartGame injects _tooltip
            if (_tooltip == null) return;

            if (TryGetOnCursorTooltipTarget(out var target))
            {
                if (_lastTooltipTarget == target) return;

                if (_lastTooltipTarget != null) _lastTooltipTarget.OnCursorExit(_tooltip);
                target.OnCursorEnter(_tooltip);
                _lastTooltipTarget = target;
            }
            else
            {
                if (_lastTooltipTarget != null) _lastTooltipTarget.OnCursorExit(_tooltip);
                _lastTooltipTarget = null;
            }
        }
        
        private bool TryGetOnCursorTooltipTarget(out GameObjectTooltipTarget target)
        {
            target = null;
            var meinCamera = Camera.main;
            if (meinCamera == null) return false;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;
            
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = meinCamera.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100)) return false;
            
            if (!hit.collider.gameObject.TryGetComponent<GameObjectTooltipTarget>(out var enterTarget)) return false;
            
            target = enterTarget;
            return true;
        }
    }
}
