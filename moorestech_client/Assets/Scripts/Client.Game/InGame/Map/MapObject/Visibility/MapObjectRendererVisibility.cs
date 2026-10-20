using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectの描画状態だけを切り替え、authoring時のRenderer状態を保持する
    ///     Toggles only map-object rendering while preserving authored Renderer states
    /// </summary>
    internal sealed class MapObjectRendererVisibility
    {
        private readonly MapObjectGameObject _mapObject;
        private readonly Renderer[] _renderers;
        private readonly bool[] _authoredEnabledStates;
        private bool _isDistanceVisible = true;
        private bool _isQualityVisible = true;

        public MapObjectRendererVisibility(MapObjectGameObject mapObject)
        {
            _mapObject = mapObject;
            _renderers = mapObject.GetComponentsInChildren<Renderer>(true);
            _authoredEnabledStates = new bool[_renderers.Length];

            // 再表示でoutline等の意図的な無効状態を壊さないよう初期値を保存する
            // Capture initial values so restoring visibility does not enable authored-off renderers such as outlines
            for (var index = 0; index < _renderers.Length; index++)
            {
                _authoredEnabledStates[index] = _renderers[index].enabled;
            }
        }

        public void SetDistanceVisible(bool visible)
        {
            _isDistanceVisible = visible;
            ApplyRenderers();
        }

        public void SetQualityVisible(bool visible)
        {
            _isQualityVisible = visible;
            ApplyRenderers();
        }

        private void ApplyRenderers()
        {
            // 破壊済み個体は再表示しない
            // A destroyed instance is never shown again
            var visible = _isDistanceVisible && _isQualityVisible && !_mapObject.IsDestroyed;

            // rootやColliderには触れずRendererだけを揃える
            // Match only Renderers without touching the root or Colliders
            for (var index = 0; index < _renderers.Length; index++)
            {
                _renderers[index].enabled = visible && _authoredEnabledStates[index];
            }
        }
    }
}
