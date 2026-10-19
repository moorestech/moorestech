using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     AABBに入る撤去可能な削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。撤去不可は件数と理由をログして除き、除外の箱はそのうち選択済みだけを返す。接続線は端点ブロックの撤去に巻き込まれるので含めない
    ///     One query returning the removable delete targets (blocks, rail edges, train cars) inside an AABB; non-removable ones are logged and dropped, a remove box keeps only the selected ones, connection lines ride on their endpoint blocks
    /// </summary>
    public class DeleteBoxTargetResolver
    {
        private readonly HashSet<string> _reportedDiagnostics = new();
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        internal DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        // 箱操作ごとに診断を一度だけ出し、次の箱では再び報告する
        // Report each diagnostic once per box operation, then allow it for the next box
        internal void ClearDiagnostics() => _reportedDiagnostics.Clear();

        internal IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max, DeleteSelection selection, DeleteSelectionIntent intent)
        {
            var targets = new List<IDeleteTarget>();
            var newlyReportedSkipped = 0;
            // ブロックの箱判定はBPコピーと同じ箱とIsOverlapを使う（複製しない）
            // Blocks use the same box and IsOverlap as blueprint copy (no duplicate rule)
            var box = BlueprintCopyTargetRule.CreateBox(min, max);
            CollectBlocks();
            CollectRails();
            CollectTrainCars();
            if (0 < newlyReportedSkipped) Debug.Log($"[DeleteBox] {newlyReportedSkipped} newly reported non-removable target(s) excluded from {min}-{max}");
            return targets;

            #region Internal

            void CollectBlocks()
            {
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values)
                {
                    if (!block.BlockPosInfo.IsOverlap(box)) continue;
                    var child = block.DeleteTarget;
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] block has no BlockGameObjectChild: {block.BlockPosInfo.OriginalPos}");
                        continue;
                    }
                    AddIfMatchesIntent(child);
                }
            }

            void CollectRails()
            {
                foreach (var chain in TrainRailObjectManager.Instance.EnumerateRailChains())
                {
                    if (chain.IsRemoving) continue;
                    var rail = chain.DeleteTarget;
                    if (rail == null)
                    {
                        LogWarningOnce($"[DeleteBox] rail chain has no DeleteTargetRail: {chain.name}");
                        continue;
                    }
                    if (!chain.IntersectsCellBox(min, max)) continue;
                    AddIfMatchesIntent(rail);
                }
            }

            void CollectTrainCars()
            {
                foreach (var car in _trainCarObjectDatastore.EnumerateTrainCars())
                {
                    if (!DeleteBoxRule.IsPointInBox(car.transform.position, min, max)) continue;
                    var child = car.DeleteTarget;
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] train car has no TrainCarEntityChildrenObject: {car.TrainCarInstanceId}");
                        continue;
                    }
                    AddIfMatchesIntent(child);
                }
            }

            // 撤去不可は理由をログして除き、除外の箱はさらに選択済みだけに絞る（表示件数・0件拒否・反映が同じ列を使う）
            // Drop non-removable targets with their reason logged and keep only selected ones for a remove box, so count, empty-check and apply share one list
            void AddIfMatchesIntent(IDeleteTarget target)
            {
                if (selection.IsTargetSelectable(target, out var denyReason))
                {
                    if (intent == DeleteSelectionIntent.Add || selection.IsSelected(target)) targets.Add(target); // silent-ok: 除外の箱で未選択は外す物が無い / 範囲内の選択済み件数のツールチップで見える
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
