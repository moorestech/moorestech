using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     指定ブロックが指定鉱脈の上に置かれた時に達成する（採掘機はドリルセル、他は占有セルのいずれかで判定）
    ///     Completes when the block is placed over the vein (drill cell for miners, any footprint cell otherwise)
    /// </summary>
    public class BlockPlaceOnVeinChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        // イベントは判定対象ブロックを積むだけで、判定と発火はティックで行う（前例: EquipItemChallengeTask）
        // Events only enqueue blocks to check; the check and completion fire on the tick (precedent: EquipItemChallengeTask)
        private readonly List<IBlock> _blocksToCheck = new();

        private readonly Guid _targetBlockGuid;
        private readonly Guid _targetVeinGuid;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new BlockPlaceOnVeinChallengeTask(challengeMasterElement);
        }

        private BlockPlaceOnVeinChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            var param = (BlockPlaceOnVeinTaskParam)challengeMasterElement.TaskParam;
            _targetBlockGuid = param.BlockGuid;
            _targetVeinGuid = param.VeinGuid;

            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        public void ManualUpdate()
        {
            if (_completed) return;

            EnqueueInitialCheckOnce();

            foreach (var block in _blocksToCheck)
            {
                if (!IsOverTargetVein(block)) continue;
                _completed = true;
                break;
            }
            _blocksToCheck.Clear();

            if (_completed) _onChallengeComplete.OnNext(this);

            #region Internal

            // チャレンジ開始前から置かれていた対象ブロックを初回ティックで回収する
            // Recover target blocks placed before this challenge started, on the first tick
            void EnqueueInitialCheckOnce()
            {
                if (_initialCheckDone) return;
                _initialCheckDone = true;
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(data.Block);
                }
            }

            bool IsOverTargetVein(IBlock block)
            {
                if (block.BlockGuid != _targetBlockGuid) return false;
                foreach (var cell in CellsToTest(block))
                {
                    foreach (var vein in ServerContext.ItemMapVeinDatastore.GetOverVeins(cell))
                    {
                        if (vein.VeinGuid == _targetVeinGuid) return true;
                    }
                }
                return false;
            }

            // 採掘機は実際に掘るドリルセルだけを見る（VanillaMinerProcessorComponent と同じ基準）
            // A miner is judged by its actual drill cell only (same rule as VanillaMinerProcessorComponent)
            IEnumerable<Vector3Int> CellsToTest(IBlock block)
            {
                var positionInfo = block.BlockPositionInfo;
                if (MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).BlockParam is IMinerParam minerParam)
                {
                    yield return positionInfo.ConvertBlockLocalToWorldCell(minerParam.DrillLocalPosition);
                    yield break;
                }
                for (var x = positionInfo.MinPos.x; x <= positionInfo.MaxPos.x; x++)
                for (var y = positionInfo.MinPos.y; y <= positionInfo.MaxPos.y; y++)
                for (var z = positionInfo.MinPos.z; z <= positionInfo.MaxPos.z; z++)
                    yield return new Vector3Int(x, y, z);
            }

            #endregion
        }

        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(block);
        }
    }
}
