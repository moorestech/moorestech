using Game.Train.RailGraph.Utility;
using System.Collections.Generic;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レールキャッシュ更新に追従するランタイム描画を管理するクラス
    ///     Manages runtime line renderers driven directly by rail cache updates
    /// </summary>
    public sealed class TrainRailObjectManager : MonoBehaviour, ISkitWorldObjectControl
    {
        public static TrainRailObjectManager Instance { get; private set; }
        [SerializeField] private BezierRailChain _railPrefab;
        private readonly Dictionary<ulong, GameObject> _railObjs = new();
        private RailGraphClientCache _cache;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            foreach (var gobj in _railObjs.Values)
            {
                if (gobj != null)
                {
                    Destroy(gobj);
                }
            }
            _railObjs.Clear();
        }

        // レール描画は自身の配下に生成されるため、スキット中は根ごと消す
        // Rail renderers are created under this transform, so a skit hides them at the root
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }

        // 描画中のレール1本をIDから引く（巻き込み赤表示用）
        // Look up a drawn rail by id (for cascade red preview)
        public bool TryGetRailChain(ulong railObjectId, out BezierRailChain chain)
        {
            chain = null;
            if (!_railObjs.TryGetValue(railObjectId, out var gobj) || gobj == null) return false;
            chain = gobj.GetComponent<BezierRailChain>();
            return chain != null;
        }

        internal void OnCacheRebuilt(RailGraphClientCache cache)
        {
            _cache = cache;
            RebuildExistingConnections();
            
        #region Internal
            
            void RebuildExistingConnections()
            {
                foreach (var gobj in _railObjs.Values)
                {
                    if (gobj != null)
                    {
                        Destroy(gobj);
                    }
                }
                _railObjs.Clear();
                
                var adjacency = _cache.ConnectNodes;
                for (var fromId = 0; fromId < adjacency.Count; fromId++)
                {
                    var edges = adjacency[fromId];
                    if (edges == null || edges.Count == 0)
                    {
                        continue;
                    }
                    
                    foreach (var (targetId, _) in edges)
                    {
                        TryActivateLine(fromId, targetId);
                    }
                }
            }
        #endregion
        }

        internal void OnConnectionUpserted(int fromNodeId, int toNodeId, RailGraphClientCache cache)
        {
            _cache = cache;
            TryActivateLine(fromNodeId, toNodeId);
        }

        internal void OnConnectionRemoved(int fromNodeId, int toNodeId, RailGraphClientCache cache)
        {
            _cache = cache;
            RemoveLine(fromNodeId, toNodeId);
            #region Internal
                void RemoveLine(int fromNodeId, int toNodeId)
                {
                    var (canonicalFrom, canonicalTo) = RailSegmentPairing.SelectCanonicalPair(fromNodeId, toNodeId);
                    var railObjectId = RailObjectIdCodec.ComputeRailObjectId(canonicalFrom, canonicalTo);
                    if (!_railObjs.TryGetValue(railObjectId, out var gobj))
                    {
                        return;
                    }
                    
                    _railObjs.Remove(railObjectId);
                    if (gobj != null)
                    {
                        UniTask.Create(async () =>
                        {
                            var bezierRailChain = gobj.GetComponent<BezierRailChain>();
                            await bezierRailChain.RemoveAnimation();
                            Destroy(gobj);
                        }).Forget();
                    }
                }
            #endregion
        }


        private void TryActivateLine(int fromNodeId, int toNodeId)
        {
            if (!HasPairedConnection(fromNodeId, toNodeId))
                return;

            var (canonicalFrom, canonicalTo) = RailSegmentPairing.SelectCanonicalPair(fromNodeId, toNodeId);
            var railObjectId = RailObjectIdCodec.ComputeRailObjectId(canonicalFrom, canonicalTo);
            if (_railObjs.ContainsKey(railObjectId))
                return;
            if (_cache == null)
                return;
            if (!_cache.TryGetNode(canonicalFrom, out var startNode))
                return;
            if (!_cache.TryGetNode(canonicalTo, out var endNode))
                return;

            var lineObject = SpawnRail($"RailLine_{canonicalFrom}_{canonicalTo}", startNode, endNode);
            RailColliderObjectIdBinder.Apply(lineObject, railObjectId);
            lineObject.transform.SetParent(transform, false);
            _railObjs[railObjectId] = lineObject;
            
            #region  Internal
            bool HasPairedConnection(int fromNodeId, int toNodeId)
            {
                if (_cache == null)
                    return false;
                
                // 双方向の接続と描画フラグを確認する
                // Check paired connection and drawable flags
                if (!_cache.TryGetRailSegment(fromNodeId, toNodeId, out var directSegment))
                    return false;
                if (!directSegment.IsDrawable)
                    return false;
                
                var oppositeSource = toNodeId ^ 1;
                var oppositeTarget = fromNodeId ^ 1;
                if (!_cache.TryGetRailSegment(oppositeSource, oppositeTarget, out var oppositeSegment))
                    return false;
                return oppositeSegment.IsDrawable;
            }
            
            GameObject SpawnRail(string name, IRailNode startNode, IRailNode endNode)
            {
                var instance = Instantiate(_railPrefab, transform);
                // 描画用の制御点を生成
                // Build render control points
                BezierUtility.BuildRenderControlPoints(startNode.FrontControlPoint, endNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
                instance.SetControlPoints(p0, p1, p2, p3);
                instance.SetRailGraphCache(_cache);
                instance.Rebuild();
                instance.PlaceAnimation().Forget();
                instance.name = name;
                return instance.gameObject;
            }
            
            #endregion
        }

    }
}
