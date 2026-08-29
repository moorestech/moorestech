using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     設置プレビュー中だけ、カメラ周辺の鉱脈AABBを半透明ボックスで実行時表示する
    ///     Renders nearby vein AABBs as translucent runtime boxes only while a placement preview is active
    /// </summary>
    public class MapVeinRangeViewService : IMapVeinRangeView, IDisposable
    {
        // 表示ボックスの親。テストとシーン確認がこの名前で残存数を数える
        // Parent of the view boxes; tests and scene inspection count survivors by this name
        public const string RootObjectName = "MapVeinRangeViewRoot";

        // カメラからこの距離内のveinだけ表示する。全vein常時表示では遠景がボックスで埋まる
        // Show only veins within this distance of the camera; showing them all would fill the distance with boxes
        private const float VisibleRadius = 96f;

        private const string BoxObjectName = "MapVeinRangeBox";

        // ボックスの見た目は単位立方体。CreatePrimitiveは必ずコライダーを付け、その破棄はフレーム末まで効かないので組み込みメッシュを直接使う
        // The box is a unit cube; CreatePrimitive always attaches a collider whose destroy only lands at frame end, so take the builtin mesh instead
        private const string BuiltinCubeMeshName = "Cube.fbx";

        private readonly MapVeinRangeBoxMaterials _boxMaterials = new();

        // 非表示のボックスは破棄せず戻す。veinは鉱脈密度に比例して増えるため作り直すと生成破棄が延々続く
        // Hidden boxes come back here instead of being destroyed; the vein count scales with vein density, so rebuilding would churn forever
        private readonly Stack<GameObject> _boxPool = new();

        private readonly List<VeinRangeEntry> _entries = new();
        private readonly Camera _mainCamera;
        private readonly Mesh _boxMesh;
        private readonly Transform _root;

        // 表示状態。既定は非表示
        // The display state; hidden by default
        private VeinDisplay _display = VeinDisplay.Hidden;

        public MapVeinRangeViewService(MapVeinAabbRegistry veinAabbRegistry, Camera mainCamera)
        {
            _mainCamera = mainCamera;
            _root = new GameObject(RootObjectName).transform;

            // メッシュがnullだとMeshFilterが空になり、例外も出ないままボックスが1つも描かれないので起動時に落とす
            // A null mesh leaves MeshFilter empty and silently draws no box at all, so fail at startup instead
            _boxMesh = Resources.GetBuiltinResource<Mesh>(BuiltinCubeMeshName);
            if (_boxMesh == null) throw new InvalidOperationException($"[MapVeinRangeViewService] 組み込みメッシュ{BuiltinCubeMeshName}をロードできません");

            // 範囲は台帳が持つ。ここは種別からマテリアルを引いた表示用の入れ物を作るだけ
            // The registry owns the ranges; here we only build the view holders with the per-kind material
            foreach (var vein in veinAabbRegistry.Veins)
            {
                var material = vein.Kind == MapVeinKind.Fluid ? _boxMaterials.FluidMaterial : _boxMaterials.ItemMaterial;
                _entries.Add(new VeinRangeEntry(vein.VeinTypeGuid, vein.Kind, vein.Bounds, material));
            }
        }

        /// <summary>
        ///     表示したい状態を受け取り、対象veinの絞り込みと描画はこのクラス内で完結させる
        ///     Takes the wanted display state; vein filtering and rendering stay inside this class
        /// </summary>
        public void SetVeinDisplay(VeinDisplay display)
        {
            _display = display;
            // 非表示への遷移を次フレームまで残さない。離脱時の残存ボックスを即座に畳む
            // Never carry a hide transition into the next frame; stray boxes fold immediately on exit
            ManualUpdate();
        }

        public void ManualUpdate()
        {
            var cameraPosition = _mainCamera.transform.position;

            foreach (var entry in _entries)
            {
                // 対象外の種別と非表示中は距離を問わず全消し。範囲内だけボックスを持たせ、外れたものはプールへ返す
                // Other kinds and the hidden state go regardless of distance; only in-range veins keep a box and the rest return to the pool
                var isVisible = IsDisplayTargetVein(entry) && IsWithinVisibleRadius(entry.Bounds, cameraPosition);
                if (isVisible) ShowEntry(entry);
                else HideEntry(entry);
            }

            #region Internal

            // 種別GUID表示中は同種の鉱脈すべて、通常は表示kindの鉱脈だけを対象にする
            // In vein-type mode every vein of that type qualifies; otherwise only veins of the displayed kind do
            bool IsDisplayTargetVein(VeinRangeEntry entry)
            {
                if (_display.VeinTypeGuid.HasValue) return entry.VeinTypeGuid == _display.VeinTypeGuid.Value;
                return entry.Kind == _display.Kind;
            }

            bool IsWithinVisibleRadius(Bounds bounds, Vector3 position)
            {
                // AABB最近点で測る。巨大なveinでも中心が遠いだけで消えないようにする
                // Measure from the closest point on the AABB so a huge vein does not vanish just because its center is far
                return (bounds.ClosestPoint(position) - position).sqrMagnitude <= VisibleRadius * VisibleRadius;
            }

            void ShowEntry(VeinRangeEntry entry)
            {
                var material = _display.VeinTypeGuid.HasValue ? _boxMaterials.HighlightMaterial : entry.Material;

                // veinは動かないので既存ボックスは置き直さない。これが再入時の二重表示を防ぐ
                // Veins never move, so an existing box is never re-placed; this is what prevents duplicates on re-entry
                if (entry.ViewObject != null)
                {
                    // 強調⇔通常の切替はマテリアル差し替えだけで済ませ、同じ材質なら触らない
                    // Switching highlight and normal only swaps the material, and an unchanged material is left alone
                    var renderer = entry.ViewObject.GetComponent<MeshRenderer>();
                    if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
                    return;
                }
                entry.ViewObject = RentBox(entry.Bounds, material);
            }

            void HideEntry(VeinRangeEntry entry)
            {
                if (entry.ViewObject == null) return;
                entry.ViewObject.SetActive(false);
                _boxPool.Push(entry.ViewObject);
                entry.ViewObject = null;
            }

            GameObject RentBox(Bounds bounds, Material material)
            {
                var box = 0 < _boxPool.Count ? _boxPool.Pop() : CreateBox();
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
                box.transform.position = bounds.center;
                box.transform.localScale = bounds.size;
                box.SetActive(true);
                return box;
            }

            GameObject CreateBox()
            {
                // コライダーを一切持たせずに組む。設置レイも採掘レイも1フレームたりとも遮らせない（純表示）
                // Build it without ever attaching a collider so it blocks neither placement nor mining rays, not even for a frame (display only)
                var box = new GameObject(BoxObjectName);
                box.transform.SetParent(_root, false);
                box.AddComponent<MeshFilter>().sharedMesh = _boxMesh;
                box.AddComponent<MeshRenderer>();
                return box;
            }

            #endregion
        }

        public void Dispose()
        {
            _boxMaterials.Dispose();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        // vein表示の固定値と状態を束ねる
        // Bundle fixed vein data and view state
        private class VeinRangeEntry
        {
            public readonly Guid VeinTypeGuid;
            public readonly MapVeinKind Kind;
            public readonly Bounds Bounds;
            public readonly Material Material;
            public GameObject ViewObject;

            public VeinRangeEntry(Guid veinTypeGuid, MapVeinKind kind, Bounds bounds, Material material)
            {
                VeinTypeGuid = veinTypeGuid;
                Kind = kind;
                Bounds = bounds;
                Material = material;
            }
        }
    }
}
