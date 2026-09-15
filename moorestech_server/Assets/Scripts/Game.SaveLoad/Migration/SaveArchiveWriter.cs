using System;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Pruning;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>マイグレーション前の原本と除去データを書き出す。既存の原本は決して壊さない</summary>
    /// <summary>Writes pre-migration originals and pruned data, never destroying an original that already exists</summary>
    public sealed class SaveArchiveWriter
    {
        // 同秒に何本まで別名を試すか。これを超えるほど短時間に何度もロードすることはない
        // How many same-second names to try; loads never repeat this often within one second
        private const int MaxCollisionRetry = 100;

        // 置き場の導出はワールドのレイアウトが持つ。セーブファイルパスが無い構成では置き場もnullのまま
        // The world layout owns where the archives go; a layout without a save file path leaves them null
        private readonly WorldDataDirectory _directory;

        public SaveArchiveWriter(WorldDataDirectory directory)
        {
            _directory = directory;

            // 置き場が決まらないまま黙って進むと、原本が一度も残らないまま変換が走る
            // Moving on silently without a location would let the migration run while the original is never kept
            if (directory.SaveBackupDirectory == null)
            {
                Debug.LogError("ワールドのセーブファイルパスが無いため、マイグレーション前の原本と除去データを退避しません。");
            }
        }

        public void WriteBackup(int worldVersion, string saveJsonText)
        {
            // 退避先が無いまま黙って進むと、変換後のセーブしか残らない
            // Moving on silently without a location would leave only the converted save behind
            if (_directory.SaveBackupDirectory == null)
            {
                Debug.LogError($"退避先が無いため版{worldVersion}のマイグレーション前セーブを退避できませんでした。");
                return;
            }

            var path = _directory.BackupSaveJsonPath(worldVersion);
            if (File.Exists(path))
            {
                // 2度目以降は最初の原本を残す。上書きすると遡り適用の起点が失われる
                // Keep the first original on later runs; overwriting would lose the anchor for retroactive migrations
                Debug.Log($"版{worldVersion}のバックアップが既にあるため上書きしません。 path={path}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, saveJsonText);
            Debug.Log($"ロード前のセーブを退避しました。 path={path}");
        }

        // ファイル名とprunedAtに同じ時刻を使う。呼び出し側で2度綴らせない
        // The file name and prunedAt share one timestamp, so the caller never spells it twice
        public void WritePruned(MissingMasterPruneOutcome outcome, DateTime utcNow)
        {
            // 除去した実体を捨てると後からの置換・返金の入力が失われるので、落ちた理由を残す
            // Dropping the removed entities would lose the input for a later replace/refund, so log why it was lost
            if (_directory.SavePrunedDirectory == null)
            {
                Debug.LogError("退避先が無いためマスタ欠損で除去したデータを保存できませんでした。");
                return;
            }

            Directory.CreateDirectory(_directory.SavePrunedDirectory);

            for (var collisionIndex = 0; collisionIndex < MaxCollisionRetry; collisionIndex++)
            {
                var path = _directory.PrunedJsonPath(utcNow, collisionIndex);
                if (File.Exists(path)) continue;

                File.WriteAllText(path, outcome.ToPrunedJson(utcNow).ToString());
                Debug.Log($"マスタ欠損で除去したデータを保存しました。 path={path}");
                return;
            }

            // 書けないまま黙って捨てると「保持する」裁定が無音で破れるので理由を残す
            // Silently dropping it would break the "keep the removed data" ruling without a trace
            Debug.LogError($"除去データの保存先が{MaxCollisionRetry}件すべて埋まっていたため保存できませんでした。 dir={_directory.SavePrunedDirectory}");
        }
    }
}
