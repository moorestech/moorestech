using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ブロック/アイテム/液体アセットをロードし、表示用アイコンを生成する
    /// Loads block/item/fluid assets and generates display icons
    /// </summary>
    public class ModAssetLoader
    {
        private readonly string _serverDirectory;
        private readonly BlockGameObject _missingBlockIdObject;
        private readonly BlockIconImagePhotographer _blockIconImagePhotographer;
        private readonly List<TrainCarIconTarget> _trainCarIconTargets;
        private readonly LoadingProgressLog _loadingProgressLog;

        private BlockGameObjectPrefabContainer _blockContainer;
        private ItemImageContainer _itemImageContainer;
        private ConnectToolImageContainer _connectToolImageContainer;
        private FluidImageContainer _fluidImageContainer;

        public ModAssetLoader(string serverDirectory, BlockGameObject missingBlockIdObject, BlockIconImagePhotographer blockIconImagePhotographer, List<TrainCarIconTarget> trainCarIconTargets, LoadingProgressLog loadingProgressLog)
        {
            _serverDirectory = serverDirectory;
            _missingBlockIdObject = missingBlockIdObject;
            _blockIconImagePhotographer = blockIconImagePhotographer;
            _trainCarIconTargets = trainCarIconTargets;
            _loadingProgressLog = loadingProgressLog;
        }

        // Addressables の並列ロードでハングするアセットを初期化直後に事前ロードする（詳細は下記）
        // Pre-load assets that hang under parallel Addressables loading, right after init (details below)
        public static async UniTask PreloadCriticalAssetsAsync()
        {
            // 【観察された事実 / Observed facts】
            // "Use Existing Build"（ローカルバンドル）モードで、並列タスク内から複数アセットを同時ロードすると
            // ItemSlotView / FluidSlotView だけが完了せずハングする（ブロック側は正常）。Addressables 初期化直後に
            // ここで事前ロードするとハングしない。ChestBlockInventory もバンドル参照維持のため Dispose しない。
            // In "Use Existing Build" mode, loading many assets concurrently inside parallel tasks makes only
            // ItemSlotView / FluidSlotView hang (block loads complete). Pre-loading here right after Addressables
            // init prevents the hang. ChestBlockInventory must also keep its bundle reference (no Dispose).
            // 根本原因は Addressables 内部のバンドルロードスケジューリング/ロックと推測されるが未特定。
            // Root cause is suspected to be Addressables' internal bundle scheduling/locking, but unidentified.
            await AddressableLoader.LoadAsync<GameObject>("Vanilla/UI/Block/ChestBlockInventory");
            await UniTask.WhenAll(ItemSlotView.LoadItemSlotViewPrefab(), FluidSlotView.LoadItemSlotViewPrefab());
            Debug.Log("[InitializeScenePipeline] critical Addressables preload completed");
        }

        public static async UniTask<List<TrainCarIconTarget>> PreloadTrainCarIconTargetsAsync()
        {
            var loadedPrefabs = new Dictionary<string, GameObject>();
            var targets = new List<TrainCarIconTarget>();
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (!loadedPrefabs.TryGetValue(trainCar.AddressablePath, out var prefab))
                {
                    prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCar.AddressablePath);
                    loadedPrefabs.Add(trainCar.AddressablePath, prefab);
                }
                targets.Add(new TrainCarIconTarget(trainCar.TrainCarGuid, prefab, trainCar.AddressablePath));
            }
            return targets;
        }

        public async UniTask<ModAssetLoadResult> RunAsync()
        {
            // ブロックとアイテムのアセットをロード
            // Load block and item assets.
            await UniTask.WhenAll(LoadBlockAssets(), LoadItemAssets(), LoadConnectToolAssets(), LoadFluidAssets());
            Debug.Log("[InitializeScenePipeline] parallel mod asset load completed");

            // ブロック・列車画像を生成
            // Generate block and train icons
            var iconLoader = new ModAssetIconLoader(_blockContainer, _trainCarIconTargets, _blockIconImagePhotographer, _loadingProgressLog);
            var iconResult = await iconLoader.RunAsync();
            Debug.Log("[InitializeScenePipeline] mod icon capture completed");

            return new ModAssetLoadResult
            {
                BlockGameObjectPrefabContainer = _blockContainer,
                ItemImageContainer = _itemImageContainer,
                BlockImageContainer = iconResult.BlockImageContainer,
                TrainCarImageContainer = iconResult.TrainCarImageContainer,
                ConnectToolImageContainer = _connectToolImageContainer,
                FluidImageContainer = _fluidImageContainer,
            };

            #region Internal

            async UniTask LoadBlockAssets()
            {
                // TODo この辺も必要な時に必要なだけロードする用にしたいなぁ
                _blockContainer = await BlockGameObjectPrefabContainer.CreateAndLoadBlockGameObjectContainer(_missingBlockIdObject);
                _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.BlockAssetsLoaded);
            }

            UniTask LoadItemAssets()
            {
                //通常のアイテム画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(_serverDirectory);
                _itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(modDirectory);
                _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.ItemImagesLoaded);
                return UniTask.CompletedTask;
            }

            UniTask LoadConnectToolAssets()
            {
                // 接続ツールアイコンをimagePathからロード
                // Load connect-tool icons from imagePath
                var modDirectory = ServerConst.CreateServerModsDirectory(_serverDirectory);
                _connectToolImageContainer = ConnectToolImageContainer.CreateAndLoadConnectToolImageContainer(modDirectory);
                _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.ConnectToolImagesLoaded);
                return UniTask.CompletedTask;
            }

            UniTask LoadFluidAssets()
            {
                //通常の液体画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(_serverDirectory);
                _fluidImageContainer = FluidImageContainer.CreateAndLoadFluidImageContainer(modDirectory);
                _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.FluidImagesLoaded);
                return UniTask.CompletedTask;
            }

            #endregion
        }
    }

    /// <summary>
    /// Mod アセットロードの結果コンテナ群
    /// Result containers from the mod asset load
    /// </summary>
    public class ModAssetLoadResult
    {
        public BlockGameObjectPrefabContainer BlockGameObjectPrefabContainer;
        public ItemImageContainer ItemImageContainer;
        public BlockImageContainer BlockImageContainer;
        public TrainCarImageContainer TrainCarImageContainer;
        public ConnectToolImageContainer ConnectToolImageContainer;
        public FluidImageContainer FluidImageContainer;
    }
}
