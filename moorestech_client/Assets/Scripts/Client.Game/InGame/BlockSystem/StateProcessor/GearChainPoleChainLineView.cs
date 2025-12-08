using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// GearChainPoleのチェーン接続を視覚的に表示するコンポーネント
    /// Component for visually displaying chain connections of GearChainPole
    /// </summary>
    public class GearChainPoleChainLineView : MonoBehaviour, IBlockGameObjectInnerComponent
    {
        private const string ChainLinePrefabAddress = "ChainLine";

        private BlockGameObject _blockGameObject;
        private GearChainPoleChainLineViewElement _chainLinePrefab;
        private bool _isPrefabLoaded;

        // 接続先座標 -> 生成したラインElement
        // Target position -> Generated line element
        private readonly Dictionary<Vector3Int, GearChainPoleChainLineViewElement> _activeLines = new();

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        /// <summary>
        /// チェーン接続の表示を更新する
        /// Update the chain connection display
        /// </summary>
        public void UpdateChainLines(Vector3Int[] partnerPositions)
        {
            // Prefabがまだロードされていなければロード
            // Load prefab if not loaded yet
            if (!_isPrefabLoaded)
            {
                LoadChainLinePrefab();
            }

            var myPosition = _blockGameObject.BlockPosInfo.OriginalPos;
            var newPositions = new HashSet<Vector3Int>(partnerPositions);

            // 削除対象を特定して削除
            // Identify and remove lines that are no longer needed
            RemoveOldLines(newPositions);

            // 新規接続のラインを作成
            // Create lines for new connections
            AddNewLines(myPosition, partnerPositions);

            #region Internal

            void LoadChainLinePrefab()
            {
                var prefab = AddressableLoader.LoadDefault<GameObject>(ChainLinePrefabAddress);
                _chainLinePrefab = prefab.GetComponent<GearChainPoleChainLineViewElement>();
                _isPrefabLoaded = true;
            }

            void RemoveOldLines(HashSet<Vector3Int> currentPositions)
            {
                var positionsToRemove = _activeLines.Keys
                    .Where(pos => !currentPositions.Contains(pos))
                    .ToList();

                foreach (var pos in positionsToRemove)
                {
                    if (_activeLines.TryGetValue(pos, out var element))
                    {
                        Destroy(element.gameObject);
                    }
                    _activeLines.Remove(pos);
                }
            }

            void AddNewLines(Vector3Int myPos, Vector3Int[] positions)
            {
                foreach (var targetPos in positions)
                {
                    if (_activeLines.ContainsKey(targetPos)) continue;
                    if (!ShouldDrawLine(myPos, targetPos)) continue;

                    var element = CreateChainLineElement(myPos, targetPos);
                    if (element != null)
                    {
                        _activeLines[targetPos] = element;
                    }
                }
            }

            #endregion
        }

        private void OnDestroy()
        {
            // すべてのラインを削除
            // Destroy all lines
            foreach (var element in _activeLines.Values)
            {
                if (element != null) Destroy(element.gameObject);
            }
            _activeLines.Clear();
        }

        /// <summary>
        /// 重複回避：座標比較で小さい方のみ描画担当
        /// Duplicate avoidance: Only the one with smaller coordinates draws
        /// </summary>
        private bool ShouldDrawLine(Vector3Int myPos, Vector3Int targetPos)
        {
            if (myPos.x != targetPos.x) return myPos.x < targetPos.x;
            if (myPos.z != targetPos.z) return myPos.z < targetPos.z;
            return myPos.y < targetPos.y;
        }

        /// <summary>
        /// チェーンラインElementを生成する
        /// Create chain line element
        /// </summary>
        private GearChainPoleChainLineViewElement CreateChainLineElement(Vector3Int myPos, Vector3Int targetPos)
        {
            if (_chainLinePrefab == null) return null;

            var element = Instantiate(_chainLinePrefab, transform);

            // 始点と終点（ブロックの中心）
            // Start and end points (block centers)
            var startWorld = new Vector3(myPos.x + 0.5f, myPos.y + 0.5f, myPos.z + 0.5f);
            var endWorld = new Vector3(targetPos.x + 0.5f, targetPos.y + 0.5f, targetPos.z + 0.5f);

            element.SetPositions(startWorld, endWorld);

            return element;
        }
    }
}
