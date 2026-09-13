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
            if (!TryParseSave(saveJsonText, out var save))
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

            // 変換を実際に行った版のときだけ原本を退避する。現在版で毎回同じ原本を書き直さない
            // Archive the original only when a migration actually ran, so the same original is not rewritten on every boot
            // 変換はメモリ上だけで進み、この時点でもディスクのsave.jsonは原本のままなので退避は取り逃さない
            // The migration happens only in memory and save.json on disk is still the original here, so nothing is missed
            if (migration.Migrated) _archiveWriter.WriteBackup(migration.FromVersion, saveJsonText);

            // 版が上がらなくてもマスタは変わるので、除去は毎回のロードで走らせる
            // The master changes even when the version does not, so pruning runs on every load
            var outcome = _pruner.Prune(migration.Save);
            if (outcome.Report.HasRemoval)
            {
                // 実世界の日時そのものを記録する用途なのでDateTimeでよい（AGENTS.mdの例外）
                // Recording a real-world timestamp is the sanctioned DateTime use (AGENTS.md exception)
                var utcNow = DateTime.UtcNow;
                _archiveWriter.WritePruned(outcome.ToPrunedJson(utcNow), utcNow);
                Debug.Log($"マスタ欠損で除去しました。 blocks={outcome.Report.RemovedBlockCount} items={outcome.Report.EmptiedItemStackCount} research={outcome.Report.RemovedResearchCount}");
            }

            _reportStore.SetReport(outcome.Report);
            return PreparedSaveJson.Ready(outcome.Save.ToString(), outcome.Report);
        }

        // 外部境界: セーブファイルは外部入力で、壊れたJSONが来る。生の例外で落とすと理由が残らない
        // External boundary: the save file is external input and may be broken JSON; a raw exception would leave no reason
        private static bool TryParseSave(string saveJsonText, out JObject save)
        {
            try
            {
                save = JObject.Parse(saveJsonText);
                return true;
            }
            catch (JsonReaderException e)
            {
                Debug.LogError($"セーブファイルのJSON解析に失敗しました。 Message : {e.Message}");
                save = null;
                return false;
            }
        }
    }
}
