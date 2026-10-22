using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.RailGraph;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.Parts
{
    /// <summary>
    /// 橋脚設置応答で返ったノードの接続エリアを、クライアントキャッシュとブロック生成が揃うまで待って解決する
    /// Resolves the connect area of the node returned by the pier placement response, waiting until both the client cache and the block spawn catch up
    /// </summary>
    public static class PlacedPierConnectAreaResolver
    {
        // 待ちがこの秒数を超えたら一度だけ警告する（待ち自体は打ち切らない）
        // Warn once when the wait exceeds this many seconds (the wait itself is never cut off)
        private const float SlowResolveWarningSeconds = 1f;

        // 解決できるまで待ち続け、キャンセルされたらnullを返す。呼び出し元は接続元を更新しない
        // Keeps waiting until resolved and returns null on cancellation; the caller then leaves the origin untouched
        public static async UniTask<IRailComponentConnectAreaCollider> Resolve(RailGraphClientCache cache, BlockGameObjectDataStore blockGameObjectDataStore, int toNodeId, CancellationToken cancellationToken)
        {
            // ノード反映はクライアント列車tickに揃うため数秒遅れうる。固定上限で捨てず、持ち替えや再選択のキャンセルまで待つ
            // Node apply is aligned to the client train tick and can lag seconds, so never give up on a fixed timeout; wait until a tool switch or re-selection cancels
            var startTime = Time.time;
            var warned = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TryResolve(out var connectArea)) return connectArea;
                if (!warned && Time.time - startTime > SlowResolveWarningSeconds)
                {
                    warned = true;
                    Debug.LogWarning($"[TrainRailConnect] Placed pier connect area is still unresolved after {SlowResolveWarningSeconds}s; keep waiting. nodeId={toNodeId}");
                }
                await UniTask.NextFrame();
            }

            // silent-ok: キャンセルは持ち替え・再選択による正常な破棄 / 起点が空のままなのはプレイヤーが画面で気づく
            // silent-ok: cancellation is a normal discard by tool switch or re-selection / the player sees the empty origin on screen
            return null;

            #region Internal

            bool TryResolve(out IRailComponentConnectAreaCollider connectArea)
            {
                connectArea = null;
                if (!cache.TryGetNode(toNodeId, out var node)) return false;
                if (!blockGameObjectDataStore.TryGetBlockGameObject((Vector3Int)node.ConnectionDestination.blockPosition, out var pierBlock)) return false;

                // 設置された橋脚の接続エリアのうち、応答が指すノードと一致するものを選ぶ
                // Pick the placed pier's connect area whose node matches the one the response points at
                connectArea = pierBlock.gameObject.GetComponentsInChildren<TrainRailConnectAreaCollider>().FirstOrDefault(area =>
                {
                    if (!cache.TryGetNodeId(area.CreateConnectionDestination(), out var nodeId)) return false;
                    if (!cache.TryGetNode(nodeId, out var clientNode)) return false;
                    return clientNode.NodeId == node.NodeId && clientNode.NodeGuid == node.NodeGuid;
                });
                return connectArea != null;
            }

            #endregion
        }
    }
}
