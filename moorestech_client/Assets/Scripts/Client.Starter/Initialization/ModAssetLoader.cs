using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Mod.Texture;
using Core.Master;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ブロック/アイテム/液体アセットをロードし、未生成のブロックアイコンを撮影する
    /// Loads block/item/fluid assets and photographs missing block icons
    /// </summary>
    public class ModAssetLoader
    {
        private readonly string _serverDirectory;
        private readonly BlockGameObject _missingBlockIdObject;
        private readonly BlockIconImagePhotographer _blockIconImagePhotographer;
        private readonly TMP_Text _loadingLog;
        private readonly System.Diagnostics.Stopwatch _loadingStopwatch;

        private BlockGameObjectPrefabContainer _blockContainer;
        private ItemImageContainer _itemImageContainer;
        private FluidImageContainer _fluidImageContainer;

        public ModAssetLoader(string serverDirectory, BlockGameObject missingBlockIdObject, BlockIconImagePhotographer blockIconImagePhotographer, TMP_Text loadingLog, System.Diagnostics.Stopwatch loadingStopwatch)
        {
            _serverDirectory = serverDirectory;
            _missingBlockIdObject = missingBlockIdObject;
            _blockIconImagePhotographer = blockIconImagePhotographer;
            _loadingLog = loadingLog;
            _loadingStopwatch = loadingStopwatch;
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
        }

        public async UniTask<ModAssetLoadResult> RunAsync()
        {
            // ブロックとアイテムのアセットをロード
            // Load block and item assets.
            await UniTask.WhenAll(LoadBlockAssets(), LoadItemAssets(), LoadFluidAssets());

            // アイテム画像がロードされていないブロックのアイテム画像をロードする
            await TakeBlockItemImage();

            return new ModAssetLoadResult
            {
                BlockGameObjectPrefabContainer = _blockContainer,
                ItemImageContainer = _itemImageContainer,
                FluidImageContainer = _fluidImageContainer,
            };

            #region Internal

            async UniTask LoadBlockAssets()
            {
                // TODo この辺も必要な時に必要なだけロードする用にしたいなぁ
                _blockContainer = await BlockGameObjectPrefabContainer.CreateAndLoadBlockGameObjectContainer(_missingBlockIdObject);
                _loadingLog.text += $"\nブロックアセットロード完了  {_loadingStopwatch.Elapsed}";
            }

            async UniTask LoadItemAssets()
            {
                //通常のアイテム画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(_serverDirectory);
                _itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(modDirectory);
                _loadingLog.text += $"\nアイテム画像ロード完了  {_loadingStopwatch.Elapsed}";
            }

            async UniTask LoadFluidAssets()
            {
                //通常の液体画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(_serverDirectory);
                _fluidImageContainer = FluidImageContainer.CreateAndLoadFluidImageContainer(modDirectory);
                _loadingLog.text += $"\n液体画像ロード完了  {_loadingStopwatch.Elapsed}";
            }

            async UniTask TakeBlockItemImage()
            {
                // スクリーンショットを取る必要があるブロックを集める
                // Collect the blocks that need to be screenshot.
                var takeBlockInfos = new List<BlockPrefabInfo>();
                var itemIds = new List<ItemId>();
                foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
                {
                    var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                    var itemViewData = _itemImageContainer.GetItemView(itemId);

                    if (itemViewData.ItemImage != null || !_blockContainer.BlockPrefabInfos.TryGetValue(blockId, out var blockObjectInfo)) continue;

                    itemIds.Add(itemId);
                    takeBlockInfos.Add(blockObjectInfo);
                }

                // アイコンを設定
                // Set the icon.
                var texture2Ds = await _blockIconImagePhotographer.TakeBlockIconImages(takeBlockInfos);
                for (var i = 0; i < itemIds.Count; i++)
                {
                    var itemViewData = new ItemViewData(texture2Ds[i], MasterHolder.ItemMaster.GetItemMaster(itemIds[i]));
                    _itemImageContainer.AddItemView(itemIds[i], itemViewData);
                }

                _loadingLog.text += $"\nブロックスクリーンショット完了  {_loadingStopwatch.Elapsed}";
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
        public FluidImageContainer FluidImageContainer;
    }
}
