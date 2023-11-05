using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Save.Interface;

namespace Server.Boot
{
    public class AutoSaveSystem
    {
        private readonly IWorldSaveDataSaver _worldSaveDataSaver;

        public AutoSaveSystem(IWorldSaveDataSaver worldSaveDataSaver)
        {
            _worldSaveDataSaver = worldSaveDataSaver;
        }

        public async Task AutoSave(CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                _worldSaveDataSaver.Save();
                if (cancellationTokenSource.Token.IsCancellationRequested) break;
            }
        }
    }
}