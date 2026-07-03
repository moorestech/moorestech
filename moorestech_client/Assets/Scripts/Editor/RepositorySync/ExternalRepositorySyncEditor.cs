using UnityEditor;
using UnityEditorInternal;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositorySyncEditor
    {
        private const string AutoSyncSessionKey = "Moorestech.ExternalRepositorySync.AutoSynced";
        private const double FocusedDetectIntervalSec = 5.0;
        private const double UnfocusedDetectIntervalSec = 30.0;

        private static double _lastDetectTime;
        private static bool _initialSyncFinished;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;

            // エディタ終了時にも外部repoのHEADをチェックして記録漏れを防ぐ
            // Also check external repository HEADs on editor quit so no change is missed
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;

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

            // フォーカス中は従来間隔、非フォーカス時は30秒間隔に下げてrebase中の差分再生成を抑える
            // Use the original interval while the editor is focused; drop to 30s when unfocused to avoid regenerating diffs during rebase cleanup
            var detectInterval = InternalEditorUtility.isApplicationActive ? FocusedDetectIntervalSec : UnfocusedDetectIntervalSec;
            if (EditorApplication.timeSinceStartup - _lastDetectTime < detectInterval) return;
            _lastDetectTime = EditorApplication.timeSinceStartup;

            // 外部repoのHEAD変更を検出してroot側の記録ファイルへ反映する
            // Detect external repository HEAD changes and reflect them into the root revision file
            ExternalRepositorySyncService.RecordCurrentCommitsIfChanged();
        }

        private static void OnEditorQuitting()
        {
            ExternalRepositorySyncService.RecordCurrentCommitsIfChanged();
        }

        private static void SyncToRecordedCommitsAtStartup()
        {
            SyncToRecordedCommits();
            _initialSyncFinished = true;
        }
    }
}
