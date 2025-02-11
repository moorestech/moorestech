using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MapObjectMineSettingsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerProcessorComponent : IUpdatableBlockComponent, IBlockSaveState
    {
        private readonly ElectricPower _requestEnergy;
        private readonly VanillaChestComponent _vanillaChestComponent;
        
        // 採掘対象
        // Mining target
        private readonly List<Guid> _miningTargetGuids;
        private readonly Dictionary<Guid, MiningTargetInfo> _miningTargetInfos;
        
        private ElectricPower _currentPower;
        
        public VanillaGearMapObjectMinerProcessorComponent(BlockPositionInfo blockPositionInfo, GearMapObjectMinerBlockParam blockParam, VanillaChestComponent vanillaChestComponent)
        {
            _requestEnergy = new ElectricPower(blockParam.RequireTorque * blockParam.RequiredRpm);
            _vanillaChestComponent = vanillaChestComponent;
            
            var minPos = blockPositionInfo.MinPos - blockParam.MiningAreaRange;
            var maxPos = blockPositionInfo.MaxPos + blockParam.MiningAreaRange;
            var mapObjects = ServerContext.MapObjectDatastore.GetWithinBoundingBox(minPos, maxPos);
            
            _miningTargetInfos = new Dictionary<Guid, MiningTargetInfo>();
            var settings = blockParam.MapObjectMineSettings.items.ToList();
            foreach (var mapObject in mapObjects)
            {
                var guid = mapObject.MapObjectGuid;
                var setting = settings.Find(x => x.MapObjectGuid == guid);
                if (setting == null)
                {
                    continue;
                }
                
                if (_miningTargetInfos.TryGetValue(guid, out var currentInfo))
                {
                    currentInfo.MapObjects.Add(mapObject);
                }
                else
                {
                    var info = new MiningTargetInfo(setting, new List<IMapObject> {mapObject});
                    _miningTargetInfos.TryAdd(guid, info);
                }
            }
            
            _miningTargetGuids = new List<Guid>();
            _miningTargetGuids.AddRange(_miningTargetInfos.Keys);
        }
        
        
        public VanillaGearMapObjectMinerProcessorComponent(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, GearMapObjectMinerBlockParam blockParam, VanillaChestComponent vanillaChestComponent) :
            this(blockPositionInfo, blockParam, vanillaChestComponent)
        {
            var itemJsons = JsonConvert.DeserializeObject<Dictionary<Guid, float>>(componentStates[SaveKey]);
            foreach (var (guid, remainingMiningTime) in itemJsons)
            {
                if (!_miningTargetInfos.TryGetValue(guid, out var info))
                {
                    continue;
                }
                
                info.RemainingMiningTime = remainingMiningTime;
            }
        }
        
        public void SupplyPower(ElectricPower currentElectricPower)
        {
            BlockException.CheckDestroy(this);
            
            _currentPower = currentElectricPower;
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);

            foreach (var targetGuid in _miningTargetGuids)
            {
                if (!_miningTargetInfos.TryGetValue(targetGuid, out var info))
                {
                    continue;
                }
                
                // 残り時間を減らす
                // Reduce remaining time
                var subTime = MachineCurrentPowerToSubSecond.GetSubSecond(_currentPower, _requestEnergy);
                info.RemainingMiningTime -= (float)subTime;
                
                // 残り時間が0以下になるまで待機
                // Wait until the remaining time is 0 or less
                if (info.RemainingMiningTime > 0)
                {
                    continue;
                }
                
                // 指定HPを攻撃し、アイテムを取得
                // Attack the specified HP and obtain an item
                info.RemainingMiningTime = info.Setting.MiningTime;
                foreach (var mapObject in info.MapObjects)
                {
                    var items = mapObject.Attack(info.Setting.AttackHp);
                    
                    // TODO 残りスロットがなくても採掘し続ける不具合があるので修正したい
                    // TODO There is a bug that continues to mine even if there are no remaining slots, so I want to fix it
                    _vanillaChestComponent.InsertItem(items);
                }
                
                // 破壊された場合は削除
                // If it is destroyed, delete it
                info.MapObjects.RemoveAll(mapObject => mapObject.IsDestroyed);
            }
        }
        
        
        public string SaveKey { get; } = typeof(VanillaGearMapObjectMinerProcessorComponent).FullName;
        public string GetSaveState()
        {
            var remainMiningTimes = new Dictionary<Guid, float>();
            foreach (var (guid, info) in _miningTargetInfos)
            {
                remainMiningTimes.Add(guid, info.RemainingMiningTime);
            }
            
            return JsonConvert.SerializeObject(remainMiningTimes);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);

            IsDestroy = true;
        }
        
        class MiningTargetInfo
        {
            public float RemainingMiningTime { get; set; }
            public MapObjectMineSettingsMasterElement Setting { get; }
            public List<IMapObject> MapObjects { get; }
            
            public MiningTargetInfo(MapObjectMineSettingsMasterElement setting, List<IMapObject> mapObjects)
            {
                MapObjects = mapObjects;
                Setting = setting;
                RemainingMiningTime = setting.MiningTime;
            }
        }
    }
}