using UnityEditor;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositorySyncEditor
    {
        private const string AutoSyncSessionKey = "Moorestech.ExternalRepositorySync.AutoSynced";
        private const double DetectIntervalSec = 5.0;

        private static double _nextDetectTime;
        private static bool _initialSyncFinished;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;

            if (!SessionState.GetBool(AutoSyncSessionKey, false))
            {
                SessionState.SetBool(AutoSyncSessionKey, true);
                _initialSyncFinished = false;
                EditorApplication.delayCall += SyncToRecordedCommitsAtStartup;
                return;
            }

            _initialSyncFinished = true;
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

        private static void OnUpdate()
        {
            if (!_initialSyncFinished) return;
            if (EditorApplication.timeSinceStartup < _nextDetectTime) return;
            _nextDetectTime = EditorApplication.timeSinceStartup + DetectIntervalSec;

            // 外部repoのHEAD変更を検出してroot側の記録ファイルへ反映する
            // Detect external repository HEAD changes and reflect them into the root revision file
            ExternalRepositorySyncService.RecordCurrentCommitsIfChanged();
        }

        private static void SyncToRecordedCommitsAtStartup()
        {
            SyncToRecordedCommits();
            _initialSyncFinished = true;
        }
    }
}
