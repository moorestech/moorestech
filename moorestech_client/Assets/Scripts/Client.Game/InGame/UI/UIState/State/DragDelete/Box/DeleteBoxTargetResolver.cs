using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     AABBに入る撤去可能な削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。撤去不可は件数と理由をログして除く。接続線は端点ブロックの撤去に巻き込まれるので含めない
    ///     One query returning the removable delete targets (blocks, rail edges, train cars) inside an AABB; non-removable ones are logged and dropped, connection lines ride on their endpoint blocks
    /// </summary>
    public class DeleteBoxTargetResolver
    {
        private readonly HashSet<string> _reportedDiagnostics = new();
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        public DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        // 箱操作ごとに診断を一度だけ出し、次の箱では再び報告する
        // Report each diagnostic once per box operation, then allow it for the next box
        internal void ClearDiagnostics() => _reportedDiagnostics.Clear();

        public IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max)
        {
            var targets = new List<IDeleteTarget>();
            var newlyReportedSkipped = 0;
            // ブロックの箱判定はBPコピーと同じ箱とIsOverlapを使う（複製しない）
            // Blocks use the same box and IsOverlap as blueprint copy (no duplicate rule)
            var box = BlueprintCopyTargetRule.CreateBox(min, max);
            CollectBlocks();
            CollectRails();
            CollectTrainCars();
            if (newlyReportedSkipped > 0) Debug.Log($"[DeleteBox] {newlyReportedSkipped} newly reported non-removable target(s) excluded from {min}-{max}");
            return targets;

            #region Internal

            void CollectBlocks()
            {
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values)
                {
                    if (!block.BlockPosInfo.IsOverlap(box)) continue;
                    var child = block.GetComponentInChildren<BlockGameObjectChild>(true);
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] block has no BlockGameObjectChild: {block.BlockPosInfo.OriginalPos}");
                        continue;
                    }
                    AddIfRemovable(child);
                }
            }

            void CollectRails()
            {
                foreach (var chain in TrainRailObjectManager.Instance.EnumerateRailChains())
                {
                    if (chain.IsRemoving) continue;
                    var mesh = chain.GetComponentInChildren<BezierRailMesh>();
                    if (mesh == null)
                    {
                        LogWarningOnce($"[DeleteBox] rail chain has no BezierRailMesh: {chain.name}");
                        continue;
                    }
                    if (!DeleteBoxRailSampler.IntersectsCellBox(mesh, min, max)) continue;
                    var rail = chain.GetComponentInChildren<DeleteTargetRail>(true);
                    if (rail == null)
                    {
                        LogWarningOnce($"[DeleteBox] rail chain has no DeleteTargetRail: {chain.name}");
                        continue;
                    }
                    AddIfRemovable(rail);
                }
            }

            void CollectTrainCars()
            {
                foreach (var car in _trainCarObjectDatastore.EnumerateTrainCars())
                {
                    if (!DeleteBoxRule.IsPointInBox(car.transform.position, min, max)) continue;
                    var child = car.GetComponentInChildren<TrainCarEntityChildrenObject>(true);
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] train car has no TrainCarEntityChildrenObject: {car.TrainCarInstanceId}");
                        continue;
                    }
                    AddIfRemovable(child);
                }
            }

            // 撤去不可は理由をログして除く（表示件数・0件拒否・追加が同じ列を使う）
            // Drop non-removable targets with their reason logged so count, empty-check and add share one list
            void AddIfRemovable(IDeleteTarget target)
            {
                if (target.IsRemovable(out var denyReason))
                {
                    targets.Add(target);
                    return;
                }
                var diagnostic = $"[DeleteBox] skipped non-removable target key={target.GetDeleteTargetKey()} reason={denyReason?.Key ?? "(none)"}";
                // silent-ok: 同一箱で報告済みの理由だけ抑止 / 最初の検出時に開発者ログへ出力済み
                if (!_reportedDiagnostics.Add(diagnostic)) return;
                newlyReportedSkipped++;
                Debug.Log(diagnostic);
            }

            void LogWarningOnce(string diagnostic)
            {
                // silent-ok: 同一箱で報告済みの欠落だけ抑止 / 最初の検出時に開発者警告へ出力済み
                if (_reportedDiagnostics.Add(diagnostic)) Debug.LogWarning(diagnostic);
            }

            #endregion
        }
    }
}
