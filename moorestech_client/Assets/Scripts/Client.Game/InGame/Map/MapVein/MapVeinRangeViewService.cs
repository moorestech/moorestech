using System;
using System.Collections.Generic;
using Client.Common;
using Client.Network.API;
using Core.Master;
using Mooresmaster.Model.MapModule;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     設置プレビュー中だけ、カメラ周辺の鉱脈AABBを半透明ボックスで実行時表示する
    ///     Renders nearby vein AABBs as translucent runtime boxes only while a placement preview is active
    /// </summary>
    public class MapVeinRangeViewService : IMapVeinRangeView
    {
        // 表示ボックスの親。テストとシーン確認がこの名前で残存数を数える
        // Parent of the view boxes; tests and scene inspection count survivors by this name
        public const string RootObjectName = "MapVeinRangeViewRoot";

        // カメラからこの距離内のveinだけ表示する。全vein常時表示では遠景がボックスで埋まる
        // Show only veins within this distance of the camera; showing them all would fill the distance with boxes
        private const float VisibleRadius = 96f;

        // 種別の色分けはveinTypeから導出する。汎用のプレビュー色とは別物なのでここで持つ
        // Type coloring derives from veinType; these are distinct from the generic preview colors so they live here
        private static readonly Color ItemVeinColor = new(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color FluidVeinColor = new(0.25f, 0.62f, 0.95f, 1f);

        private readonly List<VeinRangeEntry> _entries = new();
        private readonly Camera _mainCamera;
        private readonly Transform _root;

        public MapVeinRangeViewService(InitialHandshakeResponse handshakeResponse, Camera mainCamera)
        {
            _mainCamera = mainCamera;
            _root = new GameObject(RootObjectName).transform;

            // veinは動かないのでAABBと色は起動時に確定させ、毎フレームのmaster参照と再計算を無くす
            // Veins never move, so fix their AABB and color at startup and drop the per-frame master lookup and recomputation
            foreach (var layout in handshakeResponse.MapLayout.MapVeins)
            {
                var veinGuid = new Guid(layout.VeinGuid);
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                if (element == null) throw new InvalidOperationException($"[MapVeinRangeViewService] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                // min/maxは内包セル座標なのでmax側に1セル分足してワールドAABBにする
                // min/max are inclusive cell coords, so add one cell on the max side to build the world AABB
                var min = new Vector3(layout.MinX, layout.MinY, layout.MinZ);
                var max = new Vector3(layout.MaxX + 1, layout.MaxY + 1, layout.MaxZ + 1);
                var bounds = new Bounds();
                bounds.SetMinMax(min, max);

                _entries.Add(new VeinRangeEntry(bounds, element.VeinParam is FluidVeinParam ? FluidVeinColor : ItemVeinColor));
            }
        }

        /// <summary>
        ///     設置プレビュー中かだけを受け取り、対象veinの絞り込みと描画はこのクラス内で完結させる
        ///     Takes only whether a placement preview is active; vein filtering and rendering stay inside this class
        /// </summary>
        public void ManualUpdate(bool isPlacementPreviewing)
        {
            var cameraPosition = _mainCamera.transform.position;

            foreach (var entry in _entries)
            {
                // プレビュー外は距離を問わず全消し。範囲内だけボックスを持たせ、外れたものは即破棄する
                // Outside a preview everything goes, regardless of distance; only in-range veins keep a box and the rest are destroyed at once
                var isVisible = isPlacementPreviewing && IsWithinVisibleRadius(entry.Bounds, cameraPosition);
                if (isVisible) ShowEntry(entry);
                else HideEntry(entry);
            }

            #region Internal

            bool IsWithinVisibleRadius(Bounds bounds, Vector3 position)
            {
                // AABB最近点で測る。巨大なveinでも中心が遠いだけで消えないようにする
                // Measure from the closest point on the AABB so a huge vein does not vanish just because its center is far
                return (bounds.ClosestPoint(position) - position).sqrMagnitude <= VisibleRadius * VisibleRadius;
            }

            void ShowEntry(VeinRangeEntry entry)
            {
                // veinは動かないので既存ボックスは作り直さない。これが再入時の二重生成を防ぐ
                // Veins never move, so an existing box is never rebuilt; this is what prevents duplicates on re-entry
                if (entry.ViewObject != null) return;
                entry.ViewObject = CreateBox(entry.Bounds, entry.Color);
            }

            void HideEntry(VeinRangeEntry entry)
            {
                if (entry.ViewObject == null) return;
                Object.Destroy(entry.ViewObject);
                entry.ViewObject = null;
            }

            GameObject CreateBox(Bounds bounds, Color color)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                // 設置レイキャストや採掘レイを遮らないようコライダーを外す（純表示）
                // Strip the collider so it blocks neither placement nor mining raycasts (display only)
                Object.Destroy(cube.GetComponent<Collider>());

                // 設置プレビュー材質を複製し、種別色を適用する
                // Clone the placement preview material and tint it with the type color
                var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
                material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                cube.GetComponent<MeshRenderer>().sharedMaterial = material;

                cube.transform.SetParent(_root, false);
                cube.transform.position = bounds.center;
                cube.transform.localScale = bounds.size;
                return cube;
            }

            #endregion
        }

        // 起動時に確定するveinのAABBと色、および現在の表示ボックスを束ねる
        // Bundles a vein's startup-fixed AABB and color with its current view box
        private class VeinRangeEntry
        {
            public readonly Bounds Bounds;
            public readonly Color Color;
            public GameObject ViewObject;

            public VeinRangeEntry(Bounds bounds, Color color)
            {
                Bounds = bounds;
                Color = color;
            }
        }
    }
}
