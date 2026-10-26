using Game.Context;
using Game.SaveLoad.Snapshot;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util.BugReport
{
    // 稼働中リングの開始を、更新を所有するサーバーティック末尾へ揃える
    // Start the live ring at the server tick end that owns its updates
    internal sealed class StartWorldSnapshotRingStep : ServerTickEndTestStep
    {
        protected override void Execute()
        {
            ServerContext.GetService<WorldSnapshotRing>().Start(null, null, null);
            Debug.Log("[SnapshotRingStart] started at the server tick end");
        }
    }
}
