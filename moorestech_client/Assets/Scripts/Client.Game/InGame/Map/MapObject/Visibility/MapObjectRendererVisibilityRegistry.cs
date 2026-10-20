using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    /// ランドマークを含む全mapObjectの表示物を持ち、画質設定の表示可否を配る
    /// Holds every map object's visibility including landmarks and distributes the graphics-quality visibility
    /// </summary>
    internal sealed class MapObjectRendererVisibilityRegistry
    {
        private readonly List<MapObjectRendererVisibility> _visibilities = new();
        private bool _isQualityVisible = true;

        public void Add(MapObjectRendererVisibility visibility)
        {
            _visibilities.Add(visibility);
            visibility.SetQualityVisible(_isQualityVisible);
        }

        public void SetQualityVisible(bool visible)
        {
            _isQualityVisible = visible;
            foreach (var visibility in _visibilities) visibility.SetQualityVisible(visible);
        }
    }
}
