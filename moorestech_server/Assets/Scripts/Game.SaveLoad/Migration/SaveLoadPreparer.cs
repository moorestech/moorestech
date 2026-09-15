using System;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>ロード前の準備を1本にまとめる: 版検出→退避→変換→マスタ欠損の除去→件数の記録</summary>
    /// <summary>One place for the pre-load work: detect version, archive, migrate, prune missing masters, record counts</summary>
    public sealed class SaveLoadPreparer
    {
        private readonly SaveMigrationChain _chain;
        private readonly MissingMasterPruner _pruner;
        private readonly SaveArchiveWriter _archiveWriter;
        private readonly MissingMasterPruneReportStore _reportStore;

        public SaveLoadPreparer(SaveMigrationChain chain, MissingMasterPruner pruner, SaveArchiveWriter archiveWriter, MissingMasterPruneReportStore reportStore)
        {
            _chain = chain;
            _pruner = pruner;
            _archiveWriter = archiveWriter;
            _reportStore = reportStore;
        }

        public PreparedSaveJson Prepare(string saveJsonText)
        {
            if (!TryParseSave(out var save))
            {
                var parseReason = "セーブファイルがJSONとして読めません。";
                Debug.LogError(parseReason);
                return PreparedSaveJson.Blocked(parseReason);
            }

            var migration = _chain.Migrate(save);
            if (!migration.CanLoad)
            {
                Debug.LogError($"セーブをロードできません: {migration.BlockedReason}");
                return PreparedSaveJson.Blocked(migration.BlockedReason);
            }

            // 版が上がらないロードでも除去結果はautosaveで原本を上書きするので、除去の前に必ず原本を退避する
            // Even a load that keeps the version lets autosave overwrite the original with the pruned result, so always archive before pruning
            // 版ごとに最初の1本だけが残り、2回目以降は書き手側が上書きせずに抜ける
            // Only the first original per version is kept; the writer skips later runs without overwriting
            _archiveWriter.WriteBackup(migration.FromVersion, saveJsonText);

            // 版が上がらなくてもマスタは変わるので、除去は毎回のロードで走らせる
            // The master changes even when the version does not, so pruning runs on every load
            var outcome = _pruner.Prune(migration.Save);
            if (outcome.HasRemoval)
            {
                // 実世界の日時そのものを記録する用途なのでDateTimeでよい（AGENTS.mdの例外）
                // Recording a real-world timestamp is the sanctioned DateTime use (AGENTS.md exception)
                _archiveWriter.WritePruned(outcome, DateTime.UtcNow);
                Debug.Log($"マスタ欠損で除去しました。 blocks={outcome.Report.RemovedBlockCount} items={outcome.Report.EmptiedItemStackCount} research={outcome.Report.RemovedResearchCount}");
            }

            _reportStore.SetReport(outcome.Report);
            return PreparedSaveJson.Ready(outcome.Save.ToString());

            #region Internal

            // 外部境界: セーブファイルは外部入力で、壊れたJSONが来る。生の例外で落とすと理由が残らない
            // External boundary: the save file is external input and may be broken JSON; a raw exception would leave no reason
            bool TryParseSave(out JObject parsedSave)
            {
                try
                {
                    parsedSave = JObject.Parse(saveJsonText);
                    return true;
                }
                catch (JsonReaderException e)
                {
                    Debug.LogError($"セーブファイルのJSON解析に失敗しました。 Message : {e.Message}");
                    parsedSave = null;
                    return false;
                }
            }

            #endregion
        }
    }
}
