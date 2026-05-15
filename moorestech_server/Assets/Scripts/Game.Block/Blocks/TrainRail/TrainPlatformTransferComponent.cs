using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainPlatformTransferComponent : IBlockSaveState, IBlockStateObservable
    {
        public bool IsDestroy { get; private set; }
        public string SaveKey { get; } = typeof(TrainPlatformTransferComponent).FullName;
        public TransferMode Mode { get; private set; }

        // モード変化を通知するためのSubject
        // Subject used to notify subscribers when the transfer mode changes
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public TrainPlatformTransferComponent(TransferMode mode)
        {
            Mode = mode;
        }

        public TrainPlatformTransferComponent(Dictionary<string, string> componentStates)
        {
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<TrainPlatformTransferComponentSaveData>(serialized);
            if (saveData == null) return;

            Mode = saveData.mode;
        }

        public void SetMode(TransferMode mode)
        {
            Mode = mode;
            // モード変更をクライアントへ通知する
            // Notify subscribers (BlockSystem -> clients) about the mode change
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new TrainPlatformTransferComponentSaveData(Mode));
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            // 現在の転送モードを単一StateDetailとして返す
            // Return the current transfer mode as a single BlockStateDetail
            return new[] { TrainPlatformTransferStateDetail.CreateState(Mode) };
        }

        public enum TransferMode
        {
            LoadToTrain,
            UnloadToPlatform,
        }

        [Serializable]
        private class TrainPlatformTransferComponentSaveData
        {
            public TransferMode mode;

            public TrainPlatformTransferComponentSaveData(TransferMode mode)
            {
                this.mode = mode;
            }
        }
    }
}
