using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltSegment.Gpu;
using Client.Game.InGame.BeltSegment.Model;
using Client.Game.InGame.Context;
using CommandForgeGenerator.Command;
using Core.Master;
using Game.BeltSegment;
using UniRx;
using UnityEngine;
using VContainer.Unity;
namespace Client.Game.InGame.BeltSegment.Rendering
{
    internal sealed class BeltItemRenderer : IStartable, ITickable, ISkitWorldObjectControl, IDisposable
    {
        private const float CubeEdge = 0.3f;
        private static readonly Vector3 CenterOffset = new(0.5f, 0.48f, 0.5f);
        private readonly ClientBeltWorld world;
        private readonly CompositeDisposable subscriptions = new();
        // 欠損診断はrenderer寿命で一度。再構築時のmaterial交換では繰り返さない。
        // Diagnose each missing kind once per renderer lifetime, across material rebuilds.
        private readonly HashSet<int> diagnosedMissing = new();
        private readonly Mesh cube;
        private BeltItemMaterials materials;
        private BeltDrawBuffers buffers;
        private BeltDrawDispatch dispatch;
        private Bounds bounds;
        private bool visible = true;
        public BeltItemRenderer(ClientBeltWorld world)
        {
            this.world = world;
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }
        public void Start()
        {
            world.OnBeltWorldSnapshotApplied.Subscribe(Rebuild).AddTo(subscriptions);
            world.OnBeltWorldTickApplied.Subscribe(Advance).AddTo(subscriptions);
            // 登録順に依存せず、初期snapshot適用後の開始にも対応する。
            // Also initialize when Start follows the first accepted snapshot.
            if (world.Status == BeltStreamStatus.Running)
                RebuildCurrent(world.CaptureCpuState());

            #region Internal
            void Rebuild(BeltWorldSnapshot snapshot) => RebuildCurrent(snapshot.Simulation);
            void Advance(BeltReplayTick tick)
            {
                // 確定挿入だけを消費し、frameごとのCPU走行列scanやGPU readbackはしない。
                // Consume accepted insertions without per-frame CPU queue scans or GPU readback.
                foreach (var insertion in tick.Insertions) Register(insertion.Item.ItemId);
                dispatch.Recompute();
            }
            #endregion
        }

        private void RebuildCurrent(BeltReplaySnapshot snapshot)
        {
            dispatch?.Dispose(); buffers?.Dispose(); materials?.Dispose();
            materials = new BeltItemMaterials(MasterHolder.ItemMaster.GetItemAllIds().ToArray(), ClientContext.ItemImageContainer,
                Resources.Load<Shader>("BeltSegment/Rendering/BeltItemInstanced"), diagnosedMissing);
            var layout = new BeltDrawLayout(world.Routes);
            bounds = layout.Bounds;
            int capacity = world.Routes.Sum(route => route.Cells.Length);
            buffers = new BeltDrawBuffers(layout, capacity, materials.Kinds, cube);
            dispatch = new BeltDrawDispatch(Resources.Load<ComputeShader>("BeltSegment/Rendering/BeltItemDraw"), world.Simulation.Buffers,
                buffers, capacity, world.Routes.Length, materials.Kinds.Length, CenterOffset);
            foreach (var segment in snapshot.Segments)
            {
                foreach (var item in segment.Items) Register(item.Item.ItemId);
                if (segment.BufferedItem.HasValue) Register(segment.BufferedItem.Value.ItemId);
            }
            dispatch.Recompute();
        }

        private void Register(int kind)
        {
            materials.Register(kind, buffers.Positions, buffers.Offsets, CubeEdge);
        }
        public void Dispose()
        {
            // ゲーム終了時にも購読と描画資源をまとめて解放する。
            // Release subscriptions and draw resources when the game lifetime ends.
            subscriptions.Dispose();
            dispatch?.Dispose(); buffers?.Dispose(); materials?.Dispose();
        }
        public void SetActive(bool enable) => visible = enable;
        public void Tick()
        {
            if (!visible || world.Status != BeltStreamStatus.Running || buffers == null) return;
            // 毎frameは描画だけ。位置の更新は確定tick通知に限定する。
            // Each frame draws only; accepted tick notifications own position updates.
            for (int i = 0; i < materials.Materials.Length; i++)
            {
                if (materials.Materials[i] == null) continue;
                var parameters = new RenderParams(materials.Materials[i]) { worldBounds = bounds };
                Graphics.RenderMeshIndirect(parameters, cube, buffers.Arguments, 1, i);
            }
        }
    }
}
