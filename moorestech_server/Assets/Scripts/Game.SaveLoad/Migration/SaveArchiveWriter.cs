using System;
using System.IO;
using Game.Paths;
using Newtonsoft.Json.Linq;
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

        private readonly SaveArchiveDirectory _directory;

        public SaveArchiveWriter(SaveArchiveDirectory directory)
        {
            _directory = directory;
        }

        public void WriteBackup(int worldVersion, string saveJsonText)
        {
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
            Debug.Log($"マイグレーション前のセーブを退避しました。 path={path}");
        }

        public void WritePruned(JObject prunedJson, DateTime utcNow)
        {
            Directory.CreateDirectory(_directory.PrunedRoot);

            for (var collisionIndex = 0; collisionIndex < MaxCollisionRetry; collisionIndex++)
            {
                var path = _directory.PrunedJsonPath(utcNow, collisionIndex);
                if (File.Exists(path)) continue;

                File.WriteAllText(path, prunedJson.ToString());
                Debug.Log($"マスタ欠損で除去したデータを保存しました。 path={path}");
                return;
            }

            // 書けないまま黙って捨てると「保持する」裁定が無音で破れるので理由を残す
            // Silently dropping it would break the "keep the removed data" ruling without a trace
            Debug.LogError($"除去データの保存先が{MaxCollisionRetry}件すべて埋まっていたため保存できませんでした。 dir={_directory.PrunedRoot}");
        }
    }
}
