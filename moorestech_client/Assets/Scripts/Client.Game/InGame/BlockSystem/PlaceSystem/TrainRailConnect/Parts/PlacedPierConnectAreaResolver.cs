using System.Linq;
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
        // 橋脚の生成とノード到着を待つ上限秒数
        // Timeout seconds for waiting the pier block spawn and its node arrival
        private const float PierSpawnWaitSeconds = 1f;

        // 解決できなければnullを返す。呼び出し元は接続元を更新しない（前回の接続元がそのまま残る）
        // Returns null when unresolved; the caller then leaves the origin untouched, so the previous origin stays as it was
        public static async UniTask<IRailComponentConnectAreaCollider> Resolve(RailGraphClientCache cache, BlockGameObjectDataStore blockGameObjectDataStore, int toNodeId)
        {
            // ノード到着とブロック生成の両方をタイムアウト付きで毎フレーム確認する（前例: GearChainPoleExtendRequestSender.WaitForPlacedPole）
            // Poll both the node arrival and the block spawn every frame with a timeout (precedent: GearChainPoleExtendRequestSender.WaitForPlacedPole)
            var startTime = Time.time;
            while (Time.time - startTime < PierSpawnWaitSeconds)
            {
                if (TryResolve(out var connectArea)) return connectArea;
                await UniTask.NextFrame();
            }

            Debug.LogWarning($"[TrainRailConnect] Placed pier connect area was not resolved within {PierSpawnWaitSeconds}s. nodeId={toNodeId}");
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
