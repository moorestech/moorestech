using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    /// - ランドマークを含む表示物を保持
    /// - 画質による表示可否を配信
    /// - Retain visibility targets including landmarks
    /// - Distribute quality visibility
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
