using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
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
        private const float LineSpacing = 0.1f;

        private BlockGameObject _blockGameObject;
        private GameObject _chainLinePrefab;
        private bool _isPrefabLoaded;

        // 接続先座標 -> 生成したラインオブジェクト
        // Target position -> Generated line object
        private readonly Dictionary<Vector3Int, GameObject> _activeLines = new();

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        /// <summary>
        /// チェーン接続の表示を更新する
        /// Update the chain connection display
        /// </summary>
        public async UniTask UpdateChainLinesAsync(Vector3Int[] partnerPositions)
        {
            // Prefabがまだロードされていなければロード
            // Load prefab if not loaded yet
            if (!_isPrefabLoaded)
            {
                await LoadChainLinePrefabAsync();
            }

            var myPosition = _blockGameObject.BlockPosInfo.OriginalPos;
            var newPositions = new HashSet<Vector3Int>(partnerPositions);

            // 削除対象を特定して削除
            // Identify and remove lines that are no longer needed
            var positionsToRemove = _activeLines.Keys
                .Where(pos => !newPositions.Contains(pos))
                .ToList();

            foreach (var pos in positionsToRemove)
            {
                if (_activeLines.TryGetValue(pos, out var lineObj))
                {
                    Destroy(lineObj);
                }
                _activeLines.Remove(pos);
            }

            // 新規接続のラインを作成
            // Create lines for new connections
            foreach (var targetPos in partnerPositions)
            {
                if (_activeLines.ContainsKey(targetPos)) continue;
                if (!ShouldDrawLine(myPosition, targetPos)) continue;

                var lineObj = CreateChainLine(myPosition, targetPos);
                if (lineObj != null)
                {
                    _activeLines[targetPos] = lineObj;
                }
            }

            #region Internal

            async UniTask LoadChainLinePrefabAsync()
            {
                _chainLinePrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(ChainLinePrefabAddress);
                _isPrefabLoaded = true;
            }

            #endregion
        }

        private void OnDestroy()
        {
            // すべてのラインを削除
            // Destroy all lines
            foreach (var lineObj in _activeLines.Values)
            {
                if (lineObj != null) Destroy(lineObj);
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
        /// チェーンラインを生成する
        /// Create chain line
        /// </summary>
        private GameObject CreateChainLine(Vector3Int myPos, Vector3Int targetPos)
        {
            if (_chainLinePrefab == null) return null;

            var lineObj = Instantiate(_chainLinePrefab, transform);
            var lineRenderers = lineObj.GetComponentsInChildren<LineRenderer>();

            // 始点と終点（ブロックの中心）
            // Start and end points (block centers)
            var startWorld = new Vector3(myPos.x + 0.5f, myPos.y + 0.5f, myPos.z + 0.5f);
            var endWorld = new Vector3(targetPos.x + 0.5f, targetPos.y + 0.5f, targetPos.z + 0.5f);

            // 2本のラインを水平方向にオフセット
            // Offset two lines horizontally
            var direction = (endWorld - startWorld).normalized;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right == Vector3.zero) right = Vector3.right;

            var offset = right * (LineSpacing / 2f);

            for (var i = 0; i < lineRenderers.Length && i < 2; i++)
            {
                var lr = lineRenderers[i];
                var lineOffset = i == 0 ? offset : -offset;

                lr.positionCount = 2;
                lr.SetPosition(0, startWorld + lineOffset);
                lr.SetPosition(1, endWorld + lineOffset);
            }

            return lineObj;
        }
    }
}
