using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MapObjectMineSettingsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerProcessorComponent : IUpdatableBlockComponent, IBlockSaveState
    {
        private readonly float _requestEnergy;
        private readonly VanillaChestComponent _vanillaChestComponent;
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly float _idleTorqueRate;

        // 採掘対象
        // Mining target
        private readonly List<Guid> _miningTargetGuids;
        private readonly Dictionary<Guid, MiningTargetInfo> _miningTargetInfos;

        private float _currentPower;

        public VanillaGearMapObjectMinerProcessorComponent(BlockPositionInfo blockPositionInfo, GearMapObjectMinerBlockParam blockParam, VanillaChestComponent vanillaChestComponent, GearEnergyTransformer gearEnergyTransformer, float idleTorqueRate)
        {
            _requestEnergy = (float)(blockParam.GearConsumption.BaseTorque * blockParam.GearConsumption.BaseRpm);
            _vanillaChestComponent = vanillaChestComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _idleTorqueRate = idleTorqueRate;
            
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

            UpdateTorqueRequestRate();
        }


        public VanillaGearMapObjectMinerProcessorComponent(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, GearMapObjectMinerBlockParam blockParam, VanillaChestComponent vanillaChestComponent, GearEnergyTransformer gearEnergyTransformer, float idleTorqueRate) :
            this(blockPositionInfo, blockParam, vanillaChestComponent, gearEnergyTransformer, idleTorqueRate)
        {
            // 秒数からtickに変換して復元
            // Convert seconds back to ticks for restoration
            var itemJsons = JsonConvert.DeserializeObject<Dictionary<Guid, double>>(componentStates[SaveKey]);
            foreach (var (guid, remainingMiningSeconds) in itemJsons)
            {
                if (!_miningTargetInfos.TryGetValue(guid, out var info))
                {
                    continue;
                }

                info.RemainingMiningTicks = GameUpdater.SecondsToTicks(remainingMiningSeconds);
            }
        }
        
        public void SupplyPower(float currentPower)
        {
            BlockException.CheckDestroy(this);

            _currentPower = currentPower;
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

                // 残りtick数を減らす
                // Reduce remaining ticks
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, _requestEnergy);
                if (subTicks >= info.RemainingMiningTicks)
                {
                    // 指定HPを攻撃し、アイテムを取得
                    // Attack the specified HP and obtain an item
                    info.RemainingMiningTicks = info.DefaultMiningTicks;
                    foreach (var mapObject in info.MapObjects)
                    {
                        var items = mapObject.Attack(info.Setting.AttackHp);

                        // TODO 残りスロットがなくても採掘し続ける不具合があるので修正したい
                        // TODO There is a bug that continues to mine even if there are no remaining slots, so I want to fix it
                        _vanillaChestComponent.InsertItem(items);
                    }

                    // 破壊された場合は削除
                    // If it is destroyed, delete it
                    var removedCount = info.MapObjects.RemoveAll(mapObject => mapObject.IsDestroyed);
                    if (removedCount > 0) UpdateTorqueRequestRate();
                }
                else
                {
                    info.RemainingMiningTicks -= subTicks;
                }
            }
        }

        private void UpdateTorqueRequestRate()
        {
            // 採掘対象の有無に応じて要求トルク倍率を変更要求する
            // Push the torque request rate based on remaining mining targets
            var hasMiningTargets = _miningTargetInfos.Values.Any(info => info.MapObjects.Count > 0);
            _gearEnergyTransformer.SetTorqueRequestRate(hasMiningTargets ? 1f : _idleTorqueRate);
        }


        public string SaveKey { get; } = typeof(VanillaGearMapObjectMinerProcessorComponent).FullName;
        public string GetSaveState()
        {
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            var remainMiningSeconds = new Dictionary<Guid, double>();
            foreach (var (guid, info) in _miningTargetInfos)
            {
                remainMiningSeconds.Add(guid, GameUpdater.TicksToSeconds(info.RemainingMiningTicks));
            }

            return JsonConvert.SerializeObject(remainMiningSeconds);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);

            IsDestroy = true;
        }
    }
    public class MiningTargetInfo
    {
        public uint RemainingMiningTicks { get; set; }
        public uint DefaultMiningTicks { get; }
        public MapObjectMineSettingsMasterElement Setting { get; }
        public List<IMapObject> MapObjects { get; }

        public MiningTargetInfo(MapObjectMineSettingsMasterElement setting, List<IMapObject> mapObjects)
        {
            MapObjects = mapObjects;
            Setting = setting;
            DefaultMiningTicks = GameUpdater.SecondsToTicks(setting.MiningTime);
            RemainingMiningTicks = DefaultMiningTicks;
        }
    }
}
