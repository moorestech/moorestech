using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Pruning.Sections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// マスタから消えた参照を、セーブの節ごとの除去器でロード前のセーブJSONから取り除く
    /// Removes references that vanished from the master out of the save JSON before load, one section pruner per save section
    /// 渡したJObjectをその場で書き換え、同じインスタンスをOutcome.Saveとして返す
    /// The given JObject is rewritten in place and the very same instance comes back as Outcome.Save
    /// </summary>
    public sealed class MissingMasterPruner
    {
        // world節は最初に除去する。除去したブロックの座標を、レールに載る節の巻き添え判定へ渡すため
        // The world section is pruned first, because the pruned blocks' positions feed the collateral check of sections that sit on rails
        private readonly WorldSectionPruner _worldSectionPruner = new();

        // ロードでマスタ解決が例外になりうる節、または除去データを残すべき節の除去器
        // Pruners for sections whose load can throw on master resolution or whose removals must be archived
        private readonly IReadOnlyList<IMissingMasterSectionPruner> _sectionPruners = new IMissingMasterSectionPruner[]
        {
            new PlayerInventorySectionPruner(),
            new GameUnlockStateSectionPruner(),
            new ResearchSectionPruner(),
        };

        // レールノードは除去したブロックを座標で指す。残すと列車の位置やレール接続がロードで解決できず無音で消える
        // Rail nodes point at pruned blocks by position; left in place, trains' positions and rail connections fail to resolve on load and vanish silently
        private readonly IReadOnlyList<IRemovedBlockAwareSectionPruner> _removedBlockAwareSectionPruners = new IRemovedBlockAwareSectionPruner[]
        {
            new TrainUnitsSectionPruner(),
            new RailSegmentsSectionPruner(),
        };

        // 除去器を持たない節。マスタguidを持たないか、ロード側がマスタに無いguidを読み飛ばすもの
        // Sections with no pruner: they hold no master guid, or their loader already skips guids absent from the master
        private static readonly IReadOnlyList<string> SectionsWithoutMasterPruning = new[]
        {
            // マスタguidを持たない
            // No master guid at all
            "worldVersion", "entities", "setting", "playerRidingStates", "constructionPayers", "inventorySlotLevel",
            "cleanRoomRooms", "miningCooldowns", "currentTick", "randomState", "backfilledFields",
            // mapObjects: マップ側に無いinstanceIdをMapObjectDatastore.LoadMapObjectが読み飛ばす
            // mapObjects: MapObjectDatastore.LoadMapObject skips instance ids absent from the map
            "mapObjects",
            // challenge/currentlyActiveChallenge: ChallengeMaster.GetChallengeがnullなら読み飛ばす
            // challenge/currentlyActiveChallenge: skipped when ChallengeMaster.GetChallenge returns null
            "challenge", "currentlyActiveChallenge",
            // blueprints: ロードは一覧を保持するだけ。貼り付け時にGetBlockIdOrNullで解決する
            // blueprints: load only keeps the list; paste resolves through GetBlockIdOrNull
            "blueprints",
            // hotbarAssignments/remainingPlacementCounts/itemStackLevels: ロード側が未解決guidを空枠・破棄へ落とす
            // hotbarAssignments/remainingPlacementCounts/itemStackLevels: their loaders drop unresolved guids to empty or discard
            "hotbarAssignments", "remainingPlacementCounts", "itemStackLevels",
        };

        public MissingMasterPruneOutcome Prune(JObject save)
        {
            LogUnclassifiedSections();

            // world節の除去結果から巻き添え判定用の座標を作り、残りの節へ値として渡す
            // Build the collateral-check positions from the world section's result and hand them to the other sections as a value
            var worldResult = PruneSection(_worldSectionPruner);
            var removedWorldBlockPositions = new RemovedWorldBlockPositions(worldResult.RemovedBlocks);

            var sectionResults = new List<MissingMasterSectionPruneResult> { worldResult };
            sectionResults.AddRange(_sectionPruners.Select(PruneSection));
            sectionResults.AddRange(_removedBlockAwareSectionPruners.Select(PruneRemovedBlockAwareSection));
            return new MissingMasterPruneOutcome(save, sectionResults);

            #region Internal

            MissingMasterSectionPruneResult PruneSection(IMissingMasterSectionPruner pruner)
            {
                var section = FindSection(pruner.SaveSectionName);
                return section == null ? new MissingMasterSectionPruneResult() : pruner.Prune(section);
            }

            MissingMasterSectionPruneResult PruneRemovedBlockAwareSection(IRemovedBlockAwareSectionPruner pruner)
            {
                var section = FindSection(pruner.SaveSectionName);
                return section == null ? new MissingMasterSectionPruneResult() : pruner.Prune(section, removedWorldBlockPositions);
            }

            // 節が無いセーブもありうるが、除去が不発だった事実は読めるようにしておく
            // A save can legitimately lack a section, but the no-op still has to leave a trace
            JToken FindSection(string sectionName)
            {
                var section = save[sectionName];
                if (section != null && section.Type != JTokenType.Null) return section;

                Debug.Log($"セーブに{sectionName}節が無いため、この節のマスタ欠損除去を行いません。");
                return null;
            }

            // 分類の無い節は除去されないまま素通る。マスタguidを持つ新しい節の足し忘れに気づけるよう警告する
            // An unclassified section passes through unpruned; warn so a new section holding master guids is not forgotten
            void LogUnclassifiedSections()
            {
                foreach (var property in save.Properties())
                {
                    if (IsClassifiedSection(property.Name)) continue;
                    Debug.LogWarning($"マスタ欠損の除去器にも除去不要一覧にも無い節は除去せず素通しします。マスタguidを持つなら除去器を足してください。 section={property.Name}");
                }
            }

            #endregion
        }

        // 除去器を持つか、除去不要と宣言済みの節か
        // Whether the section has a pruner or is declared as needing none
        public bool IsClassifiedSection(string sectionName)
        {
            return _worldSectionPruner.SaveSectionName == sectionName
                   || _sectionPruners.Any(pruner => pruner.SaveSectionName == sectionName)
                   || _removedBlockAwareSectionPruners.Any(pruner => pruner.SaveSectionName == sectionName)
                   || SectionsWithoutMasterPruning.Contains(sectionName);
        }
    }
}
