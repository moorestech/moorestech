using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class BlockUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, BlockUnlockStateInfo> _infos = new();

        public BlockUnlockStateHolder()
        {
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (_infos.ContainsKey(block.BlockGuid)) continue;
                // InitialUnlockedはoptionalスキーマのためnull=falseとして扱う
                // InitialUnlocked is an optional schema field; treat null as false
                _infos.Add(block.BlockGuid, new BlockUnlockStateInfo(block.BlockGuid, block.InitialUnlocked ?? false));
            }
        }

        public void Unlock(Guid blockGuid)
        {
            if (!_infos.ContainsKey(blockGuid))
            {
                Debug.LogError($"[UnlockBlock] Block not found: {blockGuid}");
                return;
            }
            _infos[blockGuid].Unlock();
            _onUnlock.OnNext(blockGuid);
        }

        public void Load(List<BlockUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しないブロックはスキップ
                // Skip blocks that don't exist in master
                if (MasterHolder.BlockMaster.GetBlockIdOrNull(Guid.Parse(jsonObject.BlockGuid)) == null) continue;
                var state = new BlockUnlockStateInfo(jsonObject);
                _infos[state.BlockGuid] = state;
            }
        }

        public List<BlockUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new BlockUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
