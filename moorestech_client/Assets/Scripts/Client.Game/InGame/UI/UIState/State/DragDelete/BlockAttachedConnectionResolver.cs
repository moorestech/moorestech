using Client.Game.InGame.UI.UIState.State.RemovePreview;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Train.RailGraph;
using Game.Block.Interface;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ブロック撤去に巻き込まれて消える接続線・レールの解決（赤表示とUndo記録）
    ///     Resolves connection lines and rails that vanish with a block removal (red preview and undo records)
    /// </summary>
    public class BlockAttachedConnectionResolver
    {
        private readonly ConnectionLineRegistry _registry;
        private readonly RailGraphClientCache _railCache;

        // ブロックごとに赤を求めた付随物を覚え、解除は同じ集合へ行う（その間に増減した線を取り違えない）
        // Remember what each block requested red on, and release exactly that set (lines changed meanwhile are not confused)
        private readonly Dictionary<BlockGameObject, List<IRemovePreviewable>> _requested = new();
        private readonly List<(int canonicalFrom, int canonicalTo)> _edgeBuffer = new();

        public BlockAttachedConnectionResolver(ConnectionLineRegistry registry, RailGraphClientCache railCache)
        {
            _registry = registry;
            _railCache = railCache;

            // ホバー中に増減した線・再構築されたレールへ赤表示を追従させる
            // Keep the red preview following lines added/removed and rails rebuilt while hovering
            _registry.OnLineAttachmentChanged.Subscribe(RefreshLineTargets);
            _railCache.OnRebuilt.Subscribe(_ => RefreshRailTargets());
        }

        // 要求者はブロック自身。線・レールが自分でホバー・選択されていても、その赤は相手側の要求として残る
        // The requester is the block itself; a line/rail hovered or selected on its own keeps its red as that other request
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
            foreach (var edge in CollectRailEdges(block))
            {
                // 無償区間と駅内部は再設置で戻るため除外し、未同期だけ記録不能とする
                // Re-placement restores free and station-internal edges; only unsynced edges count as unrecordable
                var result = RemovedRail.Create(_railCache, edge.canonicalFrom, edge.canonicalTo);
                if (result.Outcome == RemovedRailCreateOutcome.Created) collector.Add(result.Rail);
                else if (result.Outcome != RemovedRailCreateOutcome.FreeSegment && result.Outcome != RemovedRailCreateOutcome.StationInternal) collector.AddUnrecordable($"cascaded rail {edge.canonicalFrom}->{edge.canonicalTo}: {result.Outcome}");
            }
        }

        private List<IRemovePreviewable> ResolveRailPreviews(BlockGameObject block)
        {
            var previews = new List<IRemovePreviewable>();
            foreach (var edge in CollectRailEdges(block))
            {
                // 描画対象が未解決なら赤表示の欠落を記録する
                // Report missing previews when the rendered rail cannot be resolved
                var railObjectId = RailObjectIdCodec.ComputeRailObjectId(edge.canonicalFrom, edge.canonicalTo);
                if (TrainRailObjectManager.Instance.TryGetRailChain(railObjectId, out var chain)) previews.Add(RailChainRemovePreview.Of(chain));
                else UnityEngine.Debug.LogWarning($"[RemovalPreview] rail chain not found: edge={edge.canonicalFrom}->{edge.canonicalTo} railObjectId={railObjectId}");
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
            AttachedRailEdgeEnumerator.Collect(_railCache, block.BlockPosInfo.OriginalPos, _edgeBuffer);
            return _edgeBuffer;
        }
    }
}
