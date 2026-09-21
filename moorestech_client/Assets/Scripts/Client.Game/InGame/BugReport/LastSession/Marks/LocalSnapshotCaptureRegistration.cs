using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    public static class LocalSnapshotCaptureRegistration
    {
        public static void RecordStartedLocalServer()
        {
            // 接続設定でなく、起動済みサーバーの記録実体を所有する
            // Own the running server's actual capture rather than a connection setting
            if (!ServerContext.GetService<WorldSnapshotRing>().IsActive)
            {
                Debug.Log("snapshot記録が無効のため、このsessionには保存元の所有印を付けません");
                return;
            }
            CleanExitMarker.RecordSnapshotCapture(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName,
                ServerContext.GetService<WorldDataDirectory>().SnapshotDirectory);
        }
    }
}
