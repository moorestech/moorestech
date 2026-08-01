using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using Unity.Profiling;
using UnityEngine;
using static Game.Block.Blocks.Util.ProfilerMarkerCreator;

namespace Game.Block.Blocks
{
    public class BlockSystem : IBlock
    {
        public BlockInstanceId BlockInstanceId { get; }
        public BlockId BlockId { get; }
        public Guid BlockGuid => BlockMasterElement.BlockGuid;
        public BlockMasterElement BlockMasterElement { get; }
        public IBlockComponentManager ComponentManager => _blockComponentManager;
        private readonly BlockComponentManager _blockComponentManager = new();
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<BlockState> BlockStateChange => _onBlockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        private readonly List<IUpdatableBlockComponent> _updatableComponents;
        private readonly List<IBlockStateDetail> _blockStateDetails;
        
        
        public BlockSystem(BlockInstanceId blockInstanceId, Guid blockGuid, List<IBlockComponent> blockComponents, BlockPositionInfo blockPositionInfo)
        {
            BlockInstanceId = blockInstanceId;
            BlockPositionInfo = blockPositionInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
            BlockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(BlockId);
            
            _blockComponentManager = new BlockComponentManager();
            _blockComponentManager.AddComponents(blockComponents);
            
            // 各コンポーネントのステートの変化を検知
            foreach (var blockState in _blockComponentManager.GetComponents<IBlockStateObservable>())
            {
                blockState.OnChangeBlockState.Subscribe(_ => { _onBlockStateChange.OnNext(GetBlockState()); });
            }
            
            // NOTE 他の場所からコンポーネントを追加するようになったら、このリストに追加するようにする
            _updatableComponents = _blockComponentManager.GetComponents<IUpdatableBlockComponent>();
            _blockStateDetails = _blockComponentManager.GetComponents<IBlockStateDetail>();
            
            OneBlockUpdateMarker = CreateUpdateMarker(BlockMasterElement);
        }
        
        public BlockState GetBlockState()
        {
            var detailStates = new Dictionary<string, byte[]>();
            foreach (var component in _blockStateDetails)
            {
                foreach (var detail in component.GetBlockStateDetails())
                {
                    if (!detailStates.TryAdd(detail.Key, detail.Value))
                    {
                        Debug.LogError($"同一キーのステート詳細が既に存在します。重複キー:{detail.Key}, \nブロック:{BlockMasterElement.Name} Position:{BlockPositionInfo.OriginalPos} InstanceId:{BlockInstanceId}");
                    }
                }
            }
            
            return new BlockState(detailStates);
        }
        
        public Dictionary<string,string> GetSaveState()
        {
            var result = new Dictionary<string, string>();
            
            var components = _blockComponentManager.GetComponents<IBlockSaveState>();
            foreach (var component in components)
            {
                var key = component.SaveKey;
                var value = component.GetSaveState();
                
                result.Add(key, value);
            }
            
            return result;
        }
        
        public void TickUpdate()
        {
            UpdateComponents(_updatableComponents);
        }

        private void UpdateComponents(List<IUpdatableBlockComponent> components)
        {
            BlockUpdateMarker.Begin();
            OneBlockUpdateMarker.Begin();

            foreach (var component in components)
            {
                var componentUpdateMarker = CreateComponentUpdateMarker(BlockMasterElement, component);
                componentUpdateMarker.Begin();
                component.Update();
                componentUpdateMarker.End();
            }

            OneBlockUpdateMarker.End();
            BlockUpdateMarker.End();
        }
        
        public void Destroy()
        {
            try
            {
                _blockComponentManager.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogError("ブロックの破壊に失敗しました。");
                Debug.LogException(e);
                throw;
            }
        }
        
        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return BlockInstanceId == other.BlockInstanceId;
        }
        
        private ProfilerMarker OneBlockUpdateMarker;
    }
}