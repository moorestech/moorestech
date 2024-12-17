using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.Util
{
    /// <summary>
    ///     GameObjectのマウスカーソル説明コンポーネントにマウスカーソルが乗っているかを統合的に管理するシステム
    ///     TODO 命名を変えたい
    /// </summary>
    public class AllGameObjectEnterExplainerController : MonoBehaviour
    {
        private GameObjectEnterExplainer _lastTargetExplainer;
        
        private void Awake()
        {
        }
        
        private void Update()
        {
            if (TryGetOnCursorExplainer(out var explainer))
            {
                if (_lastTargetExplainer == explainer) return;
                
                if (_lastTargetExplainer != null) _lastTargetExplainer.OnCursorExit();
                explainer.OnCursorEnter();
                _lastTargetExplainer = explainer;
            }
            else
            {
                if (_lastTargetExplainer != null) _lastTargetExplainer.OnCursorExit();
                _lastTargetExplainer = null;
            }
        }
        
        private bool TryGetOnCursorExplainer(out GameObjectEnterExplainer explainer)
        {
            explainer = null;
            if (Camera.main == null) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = Camera.main.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100)) return false;
            
            if (!hit.collider.gameObject.TryGetComponent<GameObjectEnterExplainer>(out var gameObjectEnterExplainer)) return false;
            
            explainer = gameObjectEnterExplainer;
            return true;
        }
    }
}