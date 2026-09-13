using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Game.Paths
{
    /// <summary>セーブと並べて置く保管領域（マイグレーション前の原本・除去データ）のファイル名を決める</summary>
    /// <summary>Names the files in the archives kept next to the save: pre-migration originals and pruned data</summary>
    /// <summary>置き場そのものの導出はWorldDataDirectoryが持つ。ここでは受け取った置き場を使うだけ</summary>
    /// <summary>WorldDataDirectory owns where those folders are; this type only consumes what it is given</summary>
    public class SaveArchiveDirectory
    {
        // ディレクトリはここでは作らない。書き込み側(SaveArchiveWriter)が要るときだけ作る
        // No directory is created here; the writer creates one only when it actually writes
        // 置き場が無いワールド構成ではnullのまま持つ。共有tempへ逃がすと別ワールドの原本と衝突する
        // A world layout without a save path keeps these null; a shared temp would collide with another world's original
        private readonly string _backupRoot;
        public string PrunedRoot { get; }

        private SaveArchiveDirectory(string backupRoot, string prunedRoot)
        {
            _backupRoot = backupRoot;
            PrunedRoot = prunedRoot;
        }

        // 版ごとに1本だけ原本を残す。後から置換・返金のマイグレーションを足すとき遡れる単位
        // Keeps one original per version, the unit a later replace/refund migration can go back to
        public string BackupSaveJsonPath(int worldVersion)
        {
            if (_backupRoot == null) return null;
            return Path.Combine(_backupRoot, worldVersion.ToString(), "save.json");
        }

        // コロンを含む拡張ISO形式はWindowsのファイル名に使えないため基本形式で綴る
        // The extended ISO form contains colons, which Windows filenames reject, so the basic form is used
        // 年がカルチャのカレンダーで化けないようInvariantCultureで綴る
        // Spelled with InvariantCulture so the year does not shift under a non-Gregorian calendar
        public string PrunedJsonPath(DateTime utcNow, int collisionIndex)
        {
            if (PrunedRoot == null) return null;
            var stamp = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var name = collisionIndex == 0 ? $"{stamp}.json" : $"{stamp}-{collisionIndex}.json";
            return Path.Combine(PrunedRoot, name);
        }

        // 退避物はそのワールドに属する派生データなので、ワールドのレイアウトが決めた置き場をそのまま使う
        // The archives are data derived from that one world, so the world's own layout decides where they go
        public static SaveArchiveDirectory FromWorldDataDirectory(WorldDataDirectory worldDataDirectory)
        {
            // 置き場が決まらないまま黙って進むと、原本が一度も残らないまま変換が走る
            // Moving on silently without a location would let the migration run while the original is never kept
            if (worldDataDirectory.SaveBackupDirectory == null)
            {
                Debug.LogError("ワールドのセーブファイルパスが無いため、マイグレーション前の原本と除去データを退避しません。");
            }

            return new SaveArchiveDirectory(worldDataDirectory.SaveBackupDirectory, worldDataDirectory.SavePrunedDirectory);
        }

        // テストが一時ディレクトリへ向けるための唯一の注入口。本番と同じ導出を通す
        // The one injection point letting a test aim at a temp directory, still through the production derivation
        public static SaveArchiveDirectory FromArchiveRoot(string archiveRoot)
        {
            return FromWorldDataDirectory(WorldDataDirectory.FromWorldRoot(archiveRoot));
        }
    }
}
