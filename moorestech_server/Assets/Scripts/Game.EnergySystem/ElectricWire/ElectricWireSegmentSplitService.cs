using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// 残存メンバー集合をワイヤー接続で辿り、連結成分に分解する。GearNetworkDatastore.FindComponentsと同方式
    /// Splits a surviving member set into connected components by walking wire connections, mirroring GearNetworkDatastore.FindComponents
    /// </summary>
    public static class ElectricWireSegmentSplitService
    {
        public static List<List<IElectricWireConnector>> FindComponents(IReadOnlyCollection<IElectricWireConnector> members)
        {
            // 残存配列とid→indexマップを1パスで構築。BFSは配列へのnull書き込みをvisitedマーク代わりに使う
            // Build the remaining array and id→index map in one pass; BFS below uses null-assignment as its visited marker
            var remaining = new IElectricWireConnector[members.Count];
            var idToIdx = new Dictionary<BlockInstanceId, int>(members.Count);
            var fillIndex = 0;
            foreach (var member in members)
            {
                remaining[fillIndex] = member;
                idToIdx[member.BlockInstanceId] = fillIndex;
                fillIndex++;
            }

            var components = new List<List<IElectricWireConnector>>();
            var queue = new Queue<IElectricWireConnector>();

            // 全スロットを走査し、未訪問のコネクタを新しい連結成分の起点として採用する
            // Walk every slot; any not-yet-visited connector seeds a new connected component
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;

                // 起点を成分に加える前に先にnull化し、queue経由で重複登録されるのを防ぐ
                // Null out the seed before enqueueing so it cannot be queued twice via some cycle
                remaining[i] = null;
                var component = new List<IElectricWireConnector>();
                queue.Clear();
                queue.Enqueue(start);

                while (0 < queue.Count)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    // 現在コネクタの全ワイヤー接続を辿り、残存集合内の未訪問ノードだけをキューに積む
                    // Walk every wire connection of the current connector and enqueue only unvisited nodes within the surviving set
                    foreach (var connection in current.WireConnections.Values)
                    {
                        // idToIdxに無い = 残存集合の外（削除済み・別セグメント）なので辺ごと遮断する
                        // Missing from idToIdx means outside the surviving set (removed or another segment); cut such edges entirely
                        if (!idToIdx.TryGetValue(connection.Connector.BlockInstanceId, out var idx)) continue;
                        if (remaining[idx] == null) continue;
                        remaining[idx] = null;
                        queue.Enqueue(connection.Connector);
                    }
                }

                components.Add(component);
            }

            return components;
        }
    }
}
