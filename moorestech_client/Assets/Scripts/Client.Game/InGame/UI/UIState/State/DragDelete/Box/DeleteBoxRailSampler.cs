using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     レール edge の曲線を等間隔にサンプルし、1点でも箱内なら交差とみなす
    ///     Samples a rail edge curve at even intervals; any sample inside the box counts as intersecting
    /// </summary>
    public static class DeleteBoxRailSampler
    {
        public const int SampleCount = 64;
        private static readonly List<Vector3> SampleBuffer = new(SampleCount);

        public static bool IntersectsCellBox(BezierRailChain chain, Vector3Int min, Vector3Int max)
        {
            // 各メッシュ子は同じ制御点を持つ（BezierRailChainSegments が全子へ同じ Point0..3 を渡す）ので先頭の1つで曲線を評価する
            // Every mesh child carries the same control points (BezierRailChainSegments hands all children the same Point0..3), so evaluate through the first one
            var mesh = chain.GetComponentInChildren<BezierRailMesh>();
            if (mesh == null)
            {
                Debug.LogWarning($"[DeleteBox] rail chain has no BezierRailMesh: {chain.name}");
                return false;
            }

            SampleBuffer.Clear();
            for (var i = 0; i < SampleCount; i++)
            {
                SampleBuffer.Add(BezierRailMeshData.EvaluatePosition(mesh, i / (float)(SampleCount - 1)));
            }
            return DeleteBoxRule.IsAnyPointInBox(SampleBuffer, min, max);
        }
    }
}
