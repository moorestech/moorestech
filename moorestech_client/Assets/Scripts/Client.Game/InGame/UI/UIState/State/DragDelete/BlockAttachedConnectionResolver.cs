using Client.Game.InGame.UI.UIState.State.RemovePreview;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Train.RailGraph;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ブロック撤去で消える線・レールの解決
    ///     Resolves lines and rails that vanish with a block removal
    /// </summary>
    public class BlockAttachedConnectionResolver
    {
        private readonly ConnectionLineRegistry _registry;
        private readonly RailGraphClientCache _railCache;

        // ブロックごとに赤を求めた付随物を覚え、解除は同じ集合へ行う（その間に増減した線を取り違えない）
        // Remember what each block requested red on, and release exactly that set (lines changed meanwhile are not confused)
        private readonly Dictionary<BlockGameObject, List<IRemovePreviewable>> _requested = new();
        private readonly List<(int canonicalFrom, int canonicalTo)> _edgeBuffer = new();
        private readonly List<ConnectionDestination> _destinationBuffer = new();
        private readonly List<ConnectionDestination> _unsyncedDestinationBuffer = new();

        public BlockAttachedConnectionResolver(ConnectionLineRegistry registry, RailGraphClientCache railCache)
        {
            _registry = registry;
            _railCache = railCache;

            // ホバー中の線・レール増減へ赤表示を追従
            // Keep the red preview following lines and rails while hovering
            _registry.OnLineAttachmentChanged.Subscribe(RefreshLineTargets);
            _railCache.OnRailTopologyChanged.Subscribe(_ => RefreshRailTargets());
        }

        // 要求者はブロック自身。線側の赤要求は残る
        // Requester is the block itself; the line's own red request stays
        public void RequestCascadePreview(BlockGameObject block)
        {
            if (_requested.ContainsKey(block)) return;

            var targets = new List<IRemovePreviewable>();
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) targets.Add(line);
            targets.AddRange(ResolveRailPreviews(block));

            foreach (var target in targets) target.RequestRemovePreview(block);
            _requested[block] = targets;
        }

        public void ReleaseCascadePreview(BlockGameObject block)
        {
            if (!_requested.Remove(block, out var targets)) return;
            foreach (var target in targets) ReleaseTarget(block, target);
        }

        public void CollectRemovedConnections(BlockGameObject block, RemovedObjectCollector collector)
        {
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) line.CollectRemovedObjects(collector);
            var edges = CollectRailEdges(block);
            // Undo記録から漏れる未同期端点だけ警告する
            // Warn only for unsynced destinations omitted from the undo record
            foreach (var destination in _unsyncedDestinationBuffer)
                UnityEngine.Debug.LogWarning($"[RemovalCascade] rail not recorded for undo: node not synced at {destination}");
            foreach (var edge in edges)
            {
                RemovedRail.Capture(_railCache, edge.canonicalFrom, edge.canonicalTo, RemovedRailCaptureContext.Cascade, collector);
            }
        }

        private List<IRemovePreviewable> ResolveRailPreviews(BlockGameObject block)
        {
            var previews = new List<IRemovePreviewable>();
            var edges = CollectRailEdges(block);
            // 同期途中の端点は次のトポロジ変化で取り直す
            // Retry transient destinations on the next topology change
            foreach (var destination in _unsyncedDestinationBuffer)
                UnityEngine.Debug.Log($"[RemovalPreview] rail node not synced yet: {destination}; retry on next topology change");
            foreach (var edge in edges)
            {
                // 描画対象が未解決なら欠落を記録する
                // Record a missing preview when the rail cannot be resolved
                var railObjectId = RailObjectIdCodec.ComputeRailObjectId(edge.canonicalFrom, edge.canonicalTo);
                if (TrainRailObjectManager.Instance.TryGetRailChain(railObjectId, out var chain)) previews.Add(RailChainRemovePreview.Of(chain));
                else UnityEngine.Debug.Log($"[RemovalPreview] rail chain not ready: edge={edge.canonicalFrom}->{edge.canonicalTo} railObjectId={railObjectId}; retry on next topology change");
            }
            return previews;
        }

        // 要求中のブロックに付く線の増減を赤表示へ反映する
        // Reflect lines added to or removed from a requesting block into its red preview
        private void RefreshLineTargets(BlockInstanceId changedBlockId)
        {
            foreach (var (block, targets) in _requested)
            {
                if (!block.BlockInstanceId.Equals(changedBlockId)) continue;

                for (var i = targets.Count - 1; 0 <= i; i--)
                {
                    if (targets[i] is not ConnectionLineDeleteTarget line || _registry.GetLinesAttachedTo(changedBlockId).Contains(line)) continue;
                    ReleaseTarget(block, line);
                    targets.RemoveAt(i);
                }
                foreach (var line in _registry.GetLinesAttachedTo(changedBlockId))
                {
                    if (line == null || targets.Contains(line)) continue;
                    targets.Add(line);
                    line.RequestRemovePreview(block);
                }
            }
        }

        // レール表示の再構築後、要求中ブロックのレール赤表示を取り直す
        // After rail displays are rebuilt, re-request the rail previews of requesting blocks
        private void RefreshRailTargets()
        {
            foreach (var (block, targets) in _requested)
            {
                for (var i = targets.Count - 1; 0 <= i; i--)
                {
                    if (targets[i] is not RailChainRemovePreview) continue;
                    ReleaseTarget(block, targets[i]);
                    targets.RemoveAt(i);
                }
                foreach (var preview in ResolveRailPreviews(block))
                {
                    targets.Add(preview);
                    preview.RequestRemovePreview(block);
                }
            }
        }

        private static void ReleaseTarget(BlockGameObject block, IRemovePreviewable target)
        {
            // 赤表示中に線が切れて破棄済みのことがある
            // A line may have been destroyed while red
            if (target is UnityEngine.Object unityObject && unityObject == null) return;
            target.ReleaseRemovePreview(block);
        }

        private List<(int canonicalFrom, int canonicalTo)> CollectRailEdges(BlockGameObject block)
        {
            _edgeBuffer.Clear();
            _destinationBuffer.Clear();
            _unsyncedDestinationBuffer.Clear();
            // BlockGameObject.Initialize と同じく、初期化されない非アクティブ子は対象外にする
            // Match BlockGameObject.Initialize, which skips inactive children during component initialization
            foreach (var area in block.GetComponentsInChildren<IRailComponentConnectAreaCollider>())
                _destinationBuffer.Add(area.CreateConnectionDestination());
            AttachedRailEdgeEnumerator.Collect(_railCache, _destinationBuffer, _edgeBuffer, _unsyncedDestinationBuffer);
            return _edgeBuffer;
        }
    }
}
