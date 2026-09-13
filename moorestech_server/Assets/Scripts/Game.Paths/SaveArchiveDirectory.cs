using System;
using System.IO;
using UnityEngine;

namespace Game.Paths
{
    /// <summary>セーブと並べて置く保管領域（マイグレーション前の原本・除去データ）のパスを一元定義する</summary>
    /// <summary>Owns the paths of the archives kept next to the save: pre-migration originals and pruned data</summary>
    public class SaveArchiveDirectory
    {
        // テスト経路(WorldDataDirectory.FromServerDataMap)でセーブパスが無いときの逃がし先
        // Where the archives go when the save path is absent, as on the test-only world layout
        private const string FallbackDirectoryName = "moorestech-save-archive";

        // ディレクトリはここでは作らない。書き込み側(SaveArchiveWriter)が要るときだけ作る
        // No directory is created here; the writer creates one only when it actually writes
        public string BackupRoot { get; }
        public string PrunedRoot { get; }

        private SaveArchiveDirectory(string backupRoot, string prunedRoot)
        {
            BackupRoot = backupRoot;
            PrunedRoot = prunedRoot;
        }

        // 版ごとに1本だけ原本を残す。後から置換・返金のマイグレーションを足すとき遡れる単位
        // Keeps one original per version, the unit a later replace/refund migration can go back to
        public string BackupSaveJsonPath(int worldVersion)
        {
            return Path.Combine(BackupRoot, worldVersion.ToString(), "save.json");
        }

        // コロンを含む拡張ISO形式はWindowsのファイル名に使えないため基本形式で綴る
        // The extended ISO form contains colons, which Windows filenames reject, so the basic form is used
        public string PrunedJsonPath(DateTime utcNow, int collisionIndex)
        {
            var stamp = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var name = collisionIndex == 0 ? $"{stamp}.json" : $"{stamp}-{collisionIndex}.json";
            return Path.Combine(PrunedRoot, name);
        }

        // 退避物はそのワールドに属する派生データなので、セーブファイルの隣に置く
        // The archives are data derived from that one world, so they live beside its save file
        public static SaveArchiveDirectory FromWorldDataDirectory(WorldDataDirectory worldDataDirectory)
        {
            if (worldDataDirectory.SaveJsonFilePath == null)
            {
                var fallback = Path.Combine(Path.GetTempPath(), FallbackDirectoryName);
                Debug.Log($"ワールドのセーブファイルパスが無いため、セーブ退避先を一時ディレクトリにします。 path={fallback}");
                return FromArchiveRoot(fallback);
            }

            return FromArchiveRoot(Path.GetDirectoryName(worldDataDirectory.SaveJsonFilePath));
        }

        public static SaveArchiveDirectory FromArchiveRoot(string archiveRoot)
        {
            return new SaveArchiveDirectory(
                Path.Combine(archiveRoot, "backup"),
                Path.Combine(archiveRoot, "pruned"));
        }
    }
}
