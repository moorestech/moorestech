using System;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 常時記録はセッション内の直前区間だけが意味を持つので、開始時に前セッションの残骸を消す
    // Always-on capture only means the current session's recent window, so leftovers from the previous session are removed at start
    public static class SnapshotDirectoryCleaner
    {
        public static void DeletePreviousSessionFiles(string directory)
        {
            if (!Directory.Exists(directory)) return;

            // 上書き前に所有印を失効させ、前回sessionへの誤帰属を防ぐ
            // Invalidate ownership before overwriting to prevent attribution to the previous session
            DeleteMatching(directory, WorldDataDirectory.SnapshotOwnerFileName);
            DeleteMatching(directory, WorldDataDirectory.SnapshotFileSearchPattern);
            DeleteMatching(directory, WorldDataDirectory.PacketLogFileSearchPattern);
        }

        private static void DeleteMatching(string directory, string searchPattern)
        {
            foreach (var path in Directory.GetFiles(directory, searchPattern))
            {
                // 常時記録の削除は後から追跡できる必要があるので、消したファイルと理由を必ず残す
                // Deleting always-on capture must stay auditable, so record which file went and why
                Debug.Log($"前セッションの常時記録を削除しました path:{path} 理由:再生は現セッション区間のみを対象とする");
                DeleteFile(path);
            }

            #region Internal

            void DeleteFile(string path)
            {
                // ディスク削除の失敗時は開始を止め、旧資料へ新sessionの所有印を付けない
                // Stop capture on disk deletion failure so old evidence never receives the new session's ownership
                try
                {
                    File.Delete(path);
                }
                catch (IOException e)
                {
                    Debug.LogError($"前セッションの常時記録の削除に失敗しました path:{path} message:{e.Message}");
                    throw;
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogError($"前セッションの常時記録の削除が権限で拒否されました path:{path} message:{e.Message}");
                    throw;
                }
            }

            #endregion
        }
    }
}
