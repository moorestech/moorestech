using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Mod.Texture;
using Core.Master;
using Cysharp.Threading.Tasks;
using Client.Starter.Initialization.Progress;
using UnityEngine;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ブロックと列車の表示用アイコンを撮影する
    /// Photographs display icons for blocks and train cars
    /// </summary>
    public class ModAssetIconLoader
    {
        private readonly BlockGameObjectPrefabContainer _blockContainer;
        private readonly List<TrainCarIconTarget> _trainCarIconTargets;
        private readonly BlockIconImagePhotographer _photographer;
        private readonly LoadingProgressLog _loadingProgressLog;

        public ModAssetIconLoader(BlockGameObjectPrefabContainer blockContainer, List<TrainCarIconTarget> trainCarIconTargets, BlockIconImagePhotographer photographer, LoadingProgressLog loadingProgressLog)
        {
            _blockContainer = blockContainer;
            _trainCarIconTargets = trainCarIconTargets;
            _photographer = photographer;
            _loadingProgressLog = loadingProgressLog;
        }

        public async UniTask<ModAssetIconLoadResult> RunAsync()
        {
            // バッチ実行では代替画像を登録
            // Register display placeholders without waiting for rendering in non-interactive batch runs
            if (Application.isBatchMode)
                Debug.Log("[ModAssetIconLoader] Batch mode uses placeholder icons instead of rendering.");

            // 撮影画像は BlockId 専用
            // Captured images are BlockId-specific
            var blockImageContainer = await TakeBlockImagesAsync();
            var trainCarImageContainer = await TakeTrainCarImagesAsync();
            return new ModAssetIconLoadResult(blockImageContainer, trainCarImageContainer);
        }

        private async UniTask<BlockImageContainer> TakeBlockImagesAsync()
        {
            // プレハブが存在するブロックだけを撮影対象に集める
            // Collect only blocks that have a prefab as capture targets
            var blockIds = new List<BlockId>();
            var targets = new List<BlockPrefabInfo>();
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                if (!_blockContainer.BlockPrefabInfos.TryGetValue(blockId, out var blockObjectInfo)) continue;
                blockIds.Add(blockId);
                targets.Add(blockObjectInfo);
            }

            // BlockId ごとに画像登録
            // Register images by BlockId
            var blockImageContainer = new BlockImageContainer();
            var captureIcons = !Application.isBatchMode;
            var textures = captureIcons ? await _photographer.TakeBlockIconImages(targets) : null;
            for (var i = 0; i < blockIds.Count; i++)
            {
                var blockId = blockIds[i];
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                blockImageContainer.AddBlockView(blockId, new ItemViewData(captureIcons ? textures[i] : Texture2D.whiteTexture, blockMaster.Name));
            }

            _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.BlockScreenshotsCaptured);
            return blockImageContainer;
        }

        private async UniTask<TrainCarImageContainer> TakeTrainCarImagesAsync()
        {
            // 事前ロード済み車両を撮影順のまま積む
            // Collect preloaded train cars in capture order
            var targets = new List<(GameObject prefab, string debugName)>();
            foreach (var trainCar in _trainCarIconTargets)
                targets.Add((trainCar.Prefab, trainCar.DebugName));

            // 撮影順を維持してTrainCarGuidへ画像を登録する
            // Preserve capture order while registering images by TrainCarGuid
            var trainCarImageContainer = new TrainCarImageContainer();
            var captureIcons = !Application.isBatchMode;
            var textures = captureIcons ? await _photographer.TakeIconImages(targets) : null;
            for (var i = 0; i < _trainCarIconTargets.Count; i++)
                trainCarImageContainer.AddTrainCarView(_trainCarIconTargets[i].TrainCarGuid, new ItemViewData(captureIcons ? textures[i] : Texture2D.whiteTexture, targets[i].debugName));

            _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.TrainCarScreenshotsCaptured);
            return trainCarImageContainer;
        }
    }

    public class TrainCarIconTarget
    {
        public readonly Guid TrainCarGuid;
        public readonly GameObject Prefab;
        public readonly string DebugName;

        public TrainCarIconTarget(Guid trainCarGuid, GameObject prefab, string addressablePath)
        {
            // 車両マスタにnameがないためAddressableパス末尾を表示名に使う
            // Use the Addressable path tail because train-car masters have no name
            var separatorIndex = addressablePath.LastIndexOf('/');
            TrainCarGuid = trainCarGuid;
            Prefab = prefab;
            DebugName = separatorIndex < 0 ? addressablePath : addressablePath[(separatorIndex + 1)..];
        }
    }

    public class ModAssetIconLoadResult
    {
        public readonly BlockImageContainer BlockImageContainer;
        public readonly TrainCarImageContainer TrainCarImageContainer;

        public ModAssetIconLoadResult(BlockImageContainer blockImageContainer, TrainCarImageContainer trainCarImageContainer)
        {
            BlockImageContainer = blockImageContainer;
            TrainCarImageContainer = trainCarImageContainer;
        }
    }
}
