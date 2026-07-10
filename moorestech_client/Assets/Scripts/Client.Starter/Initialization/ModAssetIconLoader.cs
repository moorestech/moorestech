using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Mod.Texture;
using Core.Master;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ブロックと列車の表示用アイコンを撮影する
    /// Photographs display icons for blocks and train cars
    /// </summary>
    public class ModAssetIconLoader
    {
        private readonly BlockGameObjectPrefabContainer _blockContainer;
        private readonly ItemImageContainer _itemImageContainer;
        private readonly BlockIconImagePhotographer _photographer;
        private readonly TMP_Text _loadingLog;
        private readonly System.Diagnostics.Stopwatch _loadingStopwatch;

        public ModAssetIconLoader(BlockGameObjectPrefabContainer blockContainer, ItemImageContainer itemImageContainer, BlockIconImagePhotographer photographer, TMP_Text loadingLog, System.Diagnostics.Stopwatch loadingStopwatch)
        {
            _blockContainer = blockContainer;
            _itemImageContainer = itemImageContainer;
            _photographer = photographer;
            _loadingLog = loadingLog;
            _loadingStopwatch = loadingStopwatch;
        }

        public async UniTask<ModAssetIconLoadResult> RunAsync()
        {
            // ブロック撮影結果をBlockId表示と不足中のItemId表示で共有する
            // Share block captures between BlockId views and missing ItemId views
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

            // BlockId画像は全件登録し、ItemId画像は未設定時だけ補完する
            // Register every BlockId image and fill ItemId images only when absent
            var blockImageContainer = new BlockImageContainer();
            var textures = await _photographer.TakeBlockIconImages(targets);
            for (var i = 0; i < blockIds.Count; i++)
            {
                var blockId = blockIds[i];
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                blockImageContainer.AddBlockView(blockId, new ItemViewData(textures[i], blockMaster.Name));

                var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                if (_itemImageContainer.GetItemView(itemId).ItemImage == null)
                    _itemImageContainer.AddItemView(itemId, new ItemViewData(textures[i], MasterHolder.ItemMaster.GetItemMaster(itemId)));
            }

            _loadingLog.text += $"\nブロックスクリーンショット完了  {_loadingStopwatch.Elapsed}";
            return blockImageContainer;
        }

        private async UniTask<TrainCarImageContainer> TakeTrainCarImagesAsync()
        {
            // 車両プレハブを直列ロードし、表示名と共に撮影対象へ積む
            // Load train-car prefabs sequentially and collect targets with display names
            var trainCarGuids = new List<Guid>();
            var targets = new List<(GameObject prefab, string debugName)>();
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCar.AddressablePath);
                trainCarGuids.Add(trainCar.TrainCarGuid);
                targets.Add((prefab, CreateDisplayName(trainCar.AddressablePath)));
            }

            // 撮影順を維持してTrainCarGuidへ画像を登録する
            // Preserve capture order while registering images by TrainCarGuid
            var trainCarImageContainer = new TrainCarImageContainer();
            var textures = await _photographer.TakeIconImages(targets);
            for (var i = 0; i < trainCarGuids.Count; i++)
                trainCarImageContainer.AddTrainCarView(trainCarGuids[i], new ItemViewData(textures[i], targets[i].debugName));

            _loadingLog.text += $"\n車両スクリーンショット完了  {_loadingStopwatch.Elapsed}";
            return trainCarImageContainer;
        }

        private static string CreateDisplayName(string addressablePath)
        {
            // 車両マスタにnameがないためAddressableパス末尾を表示名に使う
            // Use the Addressable path tail because train-car masters have no name
            var separatorIndex = addressablePath.LastIndexOf('/');
            return separatorIndex < 0 ? addressablePath : addressablePath[(separatorIndex + 1)..];
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
