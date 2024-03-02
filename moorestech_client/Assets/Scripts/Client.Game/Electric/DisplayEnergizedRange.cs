using System.Collections.Generic;
using Core.Const;
using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.PlayerInventory.Interface;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.HotBar;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIState;
using ServerServiceProvider;
using UnityEngine;
using VContainer;

namespace MainGame.Extension
{
    /// <summary>
    ///     TODO 各データにアクセスしやすいようなアクセッサを作ってそっちに乗り換える
    /// </summary>
    public class DisplayEnergizedRange : MonoBehaviour
    {
        [SerializeField] private EnergizedRangeObject rangePrefab;
        private readonly List<EnergizedRangeObject> rangeObjects = new();


        private IBlockConfig _blockConfig;
        private ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;
        private ILocalPlayerInventory _localPlayerInventory;
        private HotBarView _hotBarView;


        private bool isBlockPlaceState;

        [Inject]
        public void Construct(MoorestechServerServiceProvider moorestechServerServiceProvider, HotBarView hotBarView, UIStateControl uiStateControl, ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore,ILocalPlayerInventory localPlayerInventory)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _blockConfig = moorestechServerServiceProvider.BlockConfig;

            _localPlayerInventory = localPlayerInventory;
            _hotBarView = hotBarView;

            hotBarView.OnSelectHotBar += OnSelectHotBar;
            uiStateControl.OnStateChanged += OnStateChanged;
            chunkBlockGameObjectDataStore.OnPlaceBlock += OnPlaceBlock;
        }

        private void OnSelectHotBar(int index)
        {
            ResetRangeObject();
            CreateRangeObject();
        }

        private void OnStateChanged(UIStateEnum state)
        {
            if (isBlockPlaceState && state != UIStateEnum.GameScreen)
            {
                isBlockPlaceState = false;
                ResetRangeObject();
                return;
            }

            if (state != UIStateEnum.GameScreen) return;
            isBlockPlaceState = true;

            CreateRangeObject();
        }

        private void OnPlaceBlock(BlockGameObject blockGameObject)
        {
            if (!isBlockPlaceState) return;

            ResetRangeObject();
            CreateRangeObject();
        }


        private void ResetRangeObject()
        {
            foreach (var rangeObject in rangeObjects) Destroy(rangeObject.gameObject);
            rangeObjects.Clear();
        }


        private void CreateRangeObject()
        {
            var (isElectricalBlock, isPole) = IsDisplay();
            //電気ブロックでも電柱でもない
            if (!isElectricalBlock && !isPole) return;


            //電気系のブロックなので電柱の範囲を表示する
            foreach (var electricalPole in GetElectricalPoles())
            {
                var config = (ElectricPoleConfigParam)_blockConfig.GetBlockConfig(electricalPole.BlockId).Param;
                var range = isElectricalBlock ? config.machineConnectionRange : config.poleConnectionRange;

                var rangeObject = Instantiate(rangePrefab, electricalPole.transform.position, Quaternion.identity, transform);
                rangeObject.SetRange(range);
                rangeObjects.Add(rangeObject);
            }
        }

        private (bool isElectricalBlock, bool isPole) IsDisplay()
        {
            var hotBarSlot = _hotBarView.SelectIndex;
            var id = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)].Id;

            if (id == ItemConst.EmptyItemId) return (false, false);
            if (!_blockConfig.IsBlock(id)) return (false, false);

            var blockConfig = _blockConfig.GetBlockConfig(_blockConfig.ItemIdToBlockId(id));

            return (IsElectricalBlock(blockConfig.Type), IsPole(blockConfig.Type));
        }

        private List<BlockGameObject> GetElectricalPoles()
        {
            var resultBlocks = new List<BlockGameObject>();
            foreach (var blocks in _chunkBlockGameObjectDataStore.BlockGameObjectDictionary)
            {
                var blockType = _blockConfig.GetBlockConfig(blocks.Value.BlockId).Type;
                if (blockType != VanillaBlockType.ElectricPole) continue;

                resultBlocks.Add(blocks.Value);
            }

            return resultBlocks;
        }

        //TODO 電気系のブロックかどうか判定するロジック
        private bool IsElectricalBlock(string type)
        {
            return type is VanillaBlockType.Generator or VanillaBlockType.Machine or VanillaBlockType.Miner;
        }

        private bool IsPole(string type)
        {
            return type is VanillaBlockType.ElectricPole;
        }
    }
}