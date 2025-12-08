using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.GearChainPole;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// ギアチェーンポールの接続ラインを表示するプロセッサ
    /// Processor for displaying gear chain pole connection lines
    /// </summary>
    public class GearChainPoleStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        private const float LineOffset = 0.05f;
        private const string ChainLinePrefabAddress = "ChainLine";

        private BlockGameObject _blockGameObject;
        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private GameObject _chainLinePrefab;
        private CancellationTokenSource _cts;

        // 接続先座標 -> チェーンラインオブジェクトのマッピング
        // Mapping of connection target position -> chain line object
        private readonly Dictionary<Vector3Int, GameObject> _chainLineObjects = new();

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _blockGameObjectDataStore = FindObjectOfType<BlockGameObjectDataStore>();
            _cts = new CancellationTokenSource();

            // チェーンラインPrefabを非同期でロード
            // Load chain line prefab asynchronously
            LoadChainLinePrefabAsync(_cts.Token).Forget();
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var state = blockState.GetStateDetail<GearChainPoleStateDetail>(GearChainPoleStateDetail.BlockStateDetailKey);
            if (state == null) return;

            UpdateChainLines(state);
        }

        private async UniTaskVoid LoadChainLinePrefabAsync(CancellationToken ct)
        {
            _chainLinePrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(ChainLinePrefabAddress, ct);
        }

        private void UpdateChainLines(GearChainPoleStateDetail state)
        {
            // 新しい接続先座標のセットを作成
            // Create a set of new connection target positions
            var newPositions = new HashSet<Vector3Int>();
            if (state.PartnerBlockPositions != null)
            {
                foreach (var pos in state.PartnerBlockPositions)
                {
                    newPositions.Add(pos.Vector3Int);
                }
            }

            // 削除された接続のラインを破棄
            // Destroy lines for removed connections
            RemoveOldLines(newPositions);

            // 新しい接続のラインを追加
            // Add lines for new connections
            AddNewLines(newPositions);

            #region Internal

            void RemoveOldLines(HashSet<Vector3Int> currentPositions)
            {
                var toRemove = _chainLineObjects.Keys
                    .Where(pos => !currentPositions.Contains(pos))
                    .ToList();

                foreach (var pos in toRemove)
                {
                    if (_chainLineObjects.TryGetValue(pos, out var lineObj))
                    {
                        Destroy(lineObj);
                    }
                    _chainLineObjects.Remove(pos);
                }
            }

            void AddNewLines(HashSet<Vector3Int> currentPositions)
            {
                var myPosition = _blockGameObject.BlockPosInfo.OriginalPos;

                foreach (var targetPos in currentPositions)
                {
                    // 既に存在する場合はスキップ
                    // Skip if already exists
                    if (_chainLineObjects.ContainsKey(targetPos)) continue;

                    // 重複回避: 自分の座標が相手より小さい場合のみ描画
                    // Avoid duplication: Only draw if my position is less than target
                    if (!ShouldDrawLine(myPosition, targetPos)) continue;

                    // ラインオブジェクトを作成
                    // Create line object
                    var lineObj = CreateChainLineObject(myPosition, targetPos);
                    if (lineObj != null)
                    {
                        _chainLineObjects[targetPos] = lineObj;
                    }
                }
            }

            #endregion
        }

        private bool ShouldDrawLine(Vector3Int myPos, Vector3Int targetPos)
        {
            // 座標を比較してどちらが描画を担当するか決定
            // Compare positions to determine which side is responsible for drawing
            // X -> Z -> Y の順で比較
            if (myPos.x != targetPos.x) return myPos.x < targetPos.x;
            if (myPos.z != targetPos.z) return myPos.z < targetPos.z;
            return myPos.y < targetPos.y;
        }

        private GameObject CreateChainLineObject(Vector3Int myPosition, Vector3Int targetPosition)
        {
            // Prefabがまだロードされていない場合はスキップ
            // Skip if prefab is not loaded yet
            if (_chainLinePrefab == null) return null;

            // 接続先のBlockGameObjectを取得
            // Get the target BlockGameObject
            var targetBlockGameObject = _blockGameObjectDataStore.GetBlockGameObject(targetPosition);
            if (targetBlockGameObject == null) return null;

            // チェーンラインをインスタンス化
            // Instantiate chain line
            var chainLineObj = Instantiate(_chainLinePrefab, transform);
            chainLineObj.name = $"ChainLine_{targetPosition}";

            // 始点と終点を設定
            // Set start and end points
            var startPos = _blockGameObject.transform.position;
            var endPos = targetBlockGameObject.transform.position;

            // LineRendererを持つ子オブジェクトを取得して位置を設定
            // Get child objects with LineRenderer and set positions
            var lineRenderers = chainLineObj.GetComponentsInChildren<LineRenderer>();
            if (lineRenderers.Length >= 2)
            {
                // 2本のライン（行き・帰り）を水平にオフセット配置
                // Place two lines (forward and return) with horizontal offset
                SetLinePositions(lineRenderers[0], startPos, endPos, Vector3.right * LineOffset);
                SetLinePositions(lineRenderers[1], startPos, endPos, Vector3.left * LineOffset);
            }

            return chainLineObj;
        }

        private void SetLinePositions(LineRenderer lineRenderer, Vector3 start, Vector3 end, Vector3 offset)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start + offset);
            lineRenderer.SetPosition(1, end + offset);
            lineRenderer.useWorldSpace = true;
        }

        private void OnDestroy()
        {
            // キャンセルトークンをキャンセル
            // Cancel the cancellation token
            _cts?.Cancel();
            _cts?.Dispose();

            // すべてのラインオブジェクトを破棄
            // Destroy all line objects
            foreach (var lineObj in _chainLineObjects.Values)
            {
                if (lineObj != null)
                {
                    Destroy(lineObj);
                }
            }
            _chainLineObjects.Clear();
        }
    }
}
