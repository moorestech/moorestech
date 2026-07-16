using System.Threading;
using Game.SaveLoad.Interface;

namespace Game.SaveLoad
{
    public sealed class WorldSaveCoordinator : IWorldSaveRequest
    {
        private readonly IWorldSaveDataSaver _worldSaveDataSaver;
        private long _requestedGeneration;
        private long _completedGeneration;

        public WorldSaveCoordinator(IWorldSaveDataSaver worldSaveDataSaver)
        {
            _worldSaveDataSaver = worldSaveDataSaver;
        }

        public void RequestSave()
        {
            Interlocked.Increment(ref _requestedGeneration);
        }

        public void SaveIfRequested()
        {
            // このtickで処理する要求番号を固定し、保存中に届く要求を次回へ残す
            // Freeze the generation handled now so requests arriving during the save remain pending
            var targetGeneration = Volatile.Read(ref _requestedGeneration);
            if (targetGeneration == Volatile.Read(ref _completedGeneration)) return;

            // 保存が完了した場合だけ、固定した要求番号までを処理済みにする
            // Mark only the frozen generation complete after the save operation succeeds
            _worldSaveDataSaver.Save();
            Volatile.Write(ref _completedGeneration, targetGeneration);
        }
    }
}
