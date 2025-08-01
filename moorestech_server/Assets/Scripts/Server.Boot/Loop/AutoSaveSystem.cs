using System;
using System.Threading;
using System.Threading.Tasks;
using Game.SaveLoad.Interface;

namespace Server.Boot.Loop
{
    public class AutoSaveSystem
    {
        
        public static async Task AutoSave(IWorldSaveDataSaver worldSaveDataSaver, CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                worldSaveDataSaver.Save();
                
                if (token.IsCancellationRequested) return;
            }
        }
    }
}