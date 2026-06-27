using UnityEditor;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositorySyncEditor
    {
        private const string AutoSyncSessionKey = "Moorestech.ExternalRepositorySync.AutoSynced";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (SessionState.GetBool(AutoSyncSessionKey, false)) return;

            SessionState.SetBool(AutoSyncSessionKey, true);
            EditorApplication.delayCall += ExternalRepositorySyncService.SyncToRecordedCommits;
        }

        [MenuItem("moorestech/External Repositories/Sync To Recorded Commits")]
        public static void SyncToRecordedCommits()
        {
            ExternalRepositorySyncService.SyncToRecordedCommits();
        }

        [MenuItem("moorestech/External Repositories/Record Current Commits")]
        public static void RecordCurrentCommits()
        {
            ExternalRepositorySyncService.RecordCurrentCommits();
        }
    }
}
