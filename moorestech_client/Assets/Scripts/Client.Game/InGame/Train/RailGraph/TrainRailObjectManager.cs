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

        // 箱選択が全レール edge を走査するための列挙口
        // Enumeration entry for box selection to scan every rail edge
        public IEnumerable<BezierRailChain> EnumerateRailChains()
        {
            foreach (var gobj in _railObjs.Values)
            {
                // silent-ok: 撤去済みedgeは辞書除去とDestroyがずれる正常経路 / 箱選択の録画シナリオで件数を確認する
                if (gobj == null) continue;
                yield return gobj.GetComponent<BezierRailChain>();
            }
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
            if (!TrainRailLineFactory.HasPairedConnection(_cache, fromNodeId, toNodeId)) return;
            var (canonicalFrom, canonicalTo) = RailSegmentPairing.SelectCanonicalPair(fromNodeId, toNodeId);
            var railObjectId = RailObjectIdCodec.ComputeRailObjectId(canonicalFrom, canonicalTo);
            if (_railObjs.ContainsKey(railObjectId)) return;
            if (!_cache.TryGetNode(canonicalFrom, out var startNode)) return;
            if (!_cache.TryGetNode(canonicalTo, out var endNode)) return;

            // 制御点の生成と表示初期化は描画ファクトリーへ委譲する
            // Delegate control-point generation and view initialization to the rail factory
            var lineObject = TrainRailLineFactory.Spawn(_railPrefab, transform, _cache, startNode, endNode, canonicalFrom, canonicalTo);
            RailColliderObjectIdBinder.Apply(lineObject, railObjectId);
            _railObjs[railObjectId] = lineObject;
        }

    }
}
