using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MapObjectMineSettingsModule;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class GearMapObjectMinerComponent : IUpdatableBlockComponent
    {
        private readonly BlockPositionInfo _blockPositionInfo;
        private readonly GearMapObjectMinerBlockParam _blockParam;
        private readonly Dictionary<Guid, MapObjectMineSettingsMasterElement> _miningSettings;
        private readonly List<Guid> _miningTargetGuids;
        
        private Dictionary<Guid,List<IMapObject>> _miningTargets;
        
        private Dictionary<Guid, float> _miningCurrentTimes;
        
        
        public void Update()
        {
            foreach (var targetGuid in _miningTargetGuids)
            {
                if (!_miningTargets.ContainsKey(targetGuid))
                {
                    continue;
                }
                
                _miningCurrentTimes.TryAdd(targetGuid, 0);
                
                _miningCurrentTimes[targetGuid] += (float)GameUpdater.UpdateSecondTime;
                var setting = _miningSettings[targetGuid];
                if (!(_miningCurrentTimes[targetGuid] >= setting.MiningTime)) continue;
                
                
                _miningCurrentTimes[targetGuid] = 0;
                var target = _miningTargets[targetGuid];
                foreach (var mapObject in target)
                {
                    var items = mapObject.Attack(setting.AttackHp);
                    foreach (var item in items)
                    {
                    }
                }
            }
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}