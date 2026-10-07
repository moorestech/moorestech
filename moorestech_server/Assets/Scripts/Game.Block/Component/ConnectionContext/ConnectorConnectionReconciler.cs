using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Component.ConnectionContext
{
    internal static class ConnectorConnectionReconciler
    {
        internal static void Apply<TTarget>(Dictionary<TTarget, ConnectedInfo> current, Dictionary<TTarget, ConnectedInfo> desired)
        {
            // 同じ内容への代入も避け、列挙中の辞書versionとport実体を保持する
            // Avoid even identical assignments to preserve enumeration versions and port identities
            var removed = new List<TTarget>();
            foreach (var target in current.Keys)
                if (!desired.ContainsKey(target)) removed.Add(target);
            foreach (var target in removed) current.Remove(target);
            foreach (var (target, info) in desired)
            {
                if (current.TryGetValue(target, out var previous) &&
                    ReferenceEquals(previous.TargetBlock, info.TargetBlock) &&
                    ReferenceEquals(previous.SelfConnector, info.SelfConnector) &&
                    ReferenceEquals(previous.TargetConnector, info.TargetConnector)) continue;
                current[target] = info;
            }
        }
    }
}
