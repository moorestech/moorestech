using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    /// <summary>
    /// GameObjectのマウスカーソル説明コンポーネントにマウスカーソルが乗っているかを統合的に管理するシステム
    /// TODO 命名を買えたい
    /// </summary>
    public class AllGameObjectEnterExplainerController : MonoBehaviour
    {
        public static AllGameObjectEnterExplainerController Instance { get; private set; }
        private Dictionary<int, GameObjectEnterExplainer> _allGameObjectEnterExplainers = new ();

        private void Awake()
        {
            Instance = this;
        }

        private bool TryGetCursorOnBlock()
        {
            if (!Physics.Raycast(ray, out var hit,100)) return false;
            if (hit.collider.gameObject)
            {
                
            }
            
            return true;
        }

        public void Register(GameObjectEnterExplainer gameObjectEnterExplainer)
        {
            var id = gameObjectEnterExplainer.GetInstanceID();
            _allGameObjectEnterExplainers.Add(id,gameObjectEnterExplainer);
        }
    }
}