using System;
using System.Collections.Generic;
using MainGame.UnityView.Control;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    /// <summary>
    /// GameObjectのマウスカーソル説明コンポーネントにマウスカーソルが乗っているかを統合的に管理するシステム
    /// TODO 命名を買えたい
    /// </summary>
    public class AllGameObjectEnterExplainerController : MonoBehaviour
    {
        private int _lastOnBlockInstanceId;
        private bool _isOnBlock;

        private void Awake()
        {
        }

        private void Update()
        {
            TODO 続き
        }

        private (bool isEnter,GameObjectEnterExplainer explainer) TryGetCursorOnBlock()
        {
            if (Camera.main == null)
            {
                return (false, null);
            }
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = Camera.main.ScreenPointToRay(mousePosition);
            
            if (!Physics.Raycast(ray, out var hit,100)) return (false, null);
            if (!hit.collider.gameObject.TryGetComponent<GameObjectEnterExplainer>(out var gameObjectEnterExplainer)) return (false, null);
            
            return (true, gameObjectEnterExplainer);
        }
    }
}