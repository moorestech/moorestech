using Cysharp.Threading.Tasks;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     ペア済みレールの描画条件と曲線オブジェクト生成を担当する
    ///     Checks paired rail visibility and creates its rendered curve object
    /// </summary>
    internal static class TrainRailLineFactory
    {
        internal static bool HasPairedConnection(RailGraphClientCache cache, int fromNodeId, int toNodeId)
        {
            if (cache == null) return false;

            // 双方向の接続と描画フラグを確認する
            // Check the connections and drawable flags in both directions
            if (!cache.TryGetRailSegment(fromNodeId, toNodeId, out var directSegment)) return false;
            if (!directSegment.IsDrawable) return false;
            var oppositeSource = toNodeId ^ 1;
            var oppositeTarget = fromNodeId ^ 1;
            if (!cache.TryGetRailSegment(oppositeSource, oppositeTarget, out var oppositeSegment)) return false;
            return oppositeSegment.IsDrawable;
        }

        internal static GameObject Spawn(BezierRailChain prefab, Transform parent, RailGraphClientCache cache,
            IRailNode startNode, IRailNode endNode, int fromNodeId, int toNodeId)
        {
            var instance = Object.Instantiate(prefab, parent);

            // 描画用の制御点を生成して曲線を初期化する
            // Build render control points and initialize the curve
            BezierUtility.BuildRenderControlPoints(startNode.FrontControlPoint, endNode.BackControlPoint,
                out var p0, out var p1, out var p2, out var p3);
            instance.SetControlPoints(p0, p1, p2, p3);
            instance.SetRailGraphCache(cache);
            instance.Rebuild();
            instance.PlaceAnimation().Forget();
            instance.name = $"RailLine_{fromNodeId}_{toNodeId}";
            return instance.gameObject;
        }
    }
}
