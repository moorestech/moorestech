using System;
using System.Threading;
using System.Threading.Tasks;
using Game.SaveLoad.Interface;

namespace Server.Boot.Loop
{
    public class AutoSaveSystem
    {
        // オートセーブ周期。要求を積むだけで、実際の保存はtick末尾のWorldSaveCoordinatorが行う
        // Auto-save period; this loop only enqueues a request and the tick-end WorldSaveCoordinator performs the save
        public static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

        public static async Task AutoSave(IWorldSaveRequest worldSaveRequest, CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(AutoSaveInterval, token);
                worldSaveRequest.RequestSave();

                if (token.IsCancellationRequested) return;
            }
        }
    }
}
