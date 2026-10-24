using System.Collections.Generic;
using Client.Common;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     漏れているセルに赤い半透明の箱を出す（設置不可プレビューと同じ材質・色）
    ///     Shows translucent red boxes on leaking cells, using the not-placeable preview material and color
    /// </summary>
    public class CleanRoomLeakMarkerVisualizer
    {
        private const string MarkerName = "CleanRoomLeakMarker";
        private static readonly Vector3 MarkerScale = new(0.9f, 0.9f, 0.9f);
        private static readonly Vector3 CellCenterOffset = new(0.5f, 0.5f, 0.5f);

        // 漏れセルは表示上限32件なので、使い回しの箱も最大32個
        // Leak cells are capped at 32 for display, so at most 32 pooled boxes exist
        private readonly List<GameObject> _markers = new();
        private Material _material;

        public void Show(IReadOnlyList<Vector3IntMessagePack> cells)
        {
            while (_markers.Count < cells.Count) _markers.Add(CreateMarker());
            for (var i = 0; i < _markers.Count; i++)
            {
                var visible = i < cells.Count;
                _markers[i].SetActive(visible);
                if (visible) _markers[i].transform.position = cells[i].Vector3Int + CellCenterOffset;
            }

            #region Internal

            GameObject CreateMarker()
            {
                // 材質は初回表示時に作る（プレビュー材質のロード完了後に呼ばれるため）
                // Create the material on first show, which happens after the preview material is loaded
                if (_material == null)
                {
                    _material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
                    _material.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
                }

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = MarkerName;
                Object.Destroy(marker.GetComponent<Collider>());
                marker.transform.localScale = MarkerScale;
                marker.GetComponent<MeshRenderer>().sharedMaterial = _material;
                marker.SetActive(false);
                return marker;
            }

            #endregion
        }

        public void HideAll()
        {
            foreach (var marker in _markers) marker.SetActive(false);
        }


    }
}
