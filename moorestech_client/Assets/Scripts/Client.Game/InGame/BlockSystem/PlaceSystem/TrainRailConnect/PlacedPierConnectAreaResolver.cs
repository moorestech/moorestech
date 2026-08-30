using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.RailGraph;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 橋脚設置応答で返ったノードの接続エリアを、クライアントキャッシュへ届くまで待って解決する
    /// Resolves the connect area of the node returned by the pier placement response, waiting until it reaches the client cache
    /// </summary>
    public static class PlacedPierConnectAreaResolver
    {
        // ノード到着待ちは1秒で打ち切る。解決できなければnullを返し、呼び出し元は接続元を引き継がない
        // The wait for the node is capped at one second; an unresolved node returns null and the caller keeps no origin
        public static async UniTask<IRailComponentConnectAreaCollider> Resolve(RailGraphClientCache cache, BlockGameObjectDataStore blockGameObjectDataStore, int toNodeId)
        {
            await UniTask.WhenAny(
                UniTask.WaitForSeconds(1f),
                UniTask.WaitUntil(() => cache.TryGetNode(toNodeId, out _))
            );
            if (!cache.TryGetNode(toNodeId, out var node)) return null;

            // 設置された橋脚の接続エリアのうち、応答が指すノードと一致するものを選ぶ
            // Pick the placed pier's connect area whose node matches the one the response points at
            var pierBlock = blockGameObjectDataStore.GetBlockGameObject(node.ConnectionDestination.blockPosition);
            Debug.Log("PierBlock", pierBlock);
            return pierBlock.gameObject.GetComponentsInChildren<TrainRailConnectAreaCollider>().FirstOrDefault(area =>
            {
                if (!cache.TryGetNodeId(area.CreateConnectionDestination(), out var nodeId)) return false;
                if (!cache.TryGetNode(nodeId, out var clientNode)) return false;
                return clientNode.NodeId == node.NodeId && clientNode.NodeGuid == node.NodeGuid;
            });
        }
    }
}
