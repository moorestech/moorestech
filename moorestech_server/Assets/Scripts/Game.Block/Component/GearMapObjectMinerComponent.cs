using System.Collections.Generic;
using Game.Context;
using Game.MapObject.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface;
using Mooresmaster.Model;
using UnityEngine;

namespace Game.Block.Component
{
    public class GearMapObjectMinerComponent : IBlockComponent
    {
        private readonly int _teethCount;
        private readonly float _requireTorque;
        private readonly float _requiredRpm;
        private readonly Vector3Int _miningAreaRange;
        private readonly Vector3Int _miningAreaOffset;
        private readonly MapObjectMineSettings _mineSettings;
        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IMapObjectFactory _mapObjectFactory;
        private readonly IBlockPositionInfo _blockPositionInfo;
        
        private readonly List<IMapObject> _miningTargets = new();
        private float _currentTime;

        public GearMapObjectMinerComponent(
            int teethCount,
            float requireTorque,
            float requiredRpm,
            Vector3Int miningAreaRange,
            Vector3Int miningAreaOffset,
            MapObjectMineSettings mineSettings,
            IMapObjectDatastore mapObjectDatastore,
            IMapObjectFactory mapObjectFactory,
            IComponentManager componentManager,
            IBlockPositionInfo blockPositionInfo)
        {
            _teethCount = teethCount;
            _requireTorque = requireTorque;
            _requiredRpm = requiredRpm;
            _miningAreaRange = miningAreaRange;
            _miningAreaOffset = miningAreaOffset;
            _mineSettings = mineSettings;
            _mapObjectDatastore = mapObjectDatastore;
            _mapObjectFactory = mapObjectFactory;
            _blockPositionInfo = blockPositionInfo;
        }

        public void Update(float deltaTime)
        {
            if (!CheckPowerCondition()) return;
            
            _currentTime += deltaTime;
            
            if (_currentTime < GetMiningInterval()) return;
            
            _currentTime = 0;
            MiningExecution();
        }

        private bool CheckPowerCondition()
        {
            // 歯車エネルギーシステムとの連携処理を実装
            // 必要なトルクと回転数を満たしているかチェック
            return true;
        }

        private float GetMiningInterval()
        {
            // 採掘間隔を計算するロジック
            return 1.0f;
        }

        private void MiningExecution()
        {
            UpdateMiningTargets();
            
            foreach (var target in _miningTargets.ToArray())
            {
                var damage = CalculateDamage();
                target.Damage(damage);
                
                if (target.IsDestroyed)
                {
                    HandleDestroyedObject(target);
                }
            }
        }

        private void UpdateMiningTargets()
        {
            _miningTargets.Clear();
            var centerPos = _blockPositionInfo.OriginalPos + _miningAreaOffset;
            
            foreach (var entity in _mapObjectDatastore.MapObjects)
            {
                if (IsInMiningArea(entity.Position, centerPos))
                {
                    _miningTargets.Add(entity);
                }
            }
        }

        private bool IsInMiningArea(Vector3Int targetPos, Vector3Int centerPos)
        {
            var min = centerPos - _miningAreaRange;
            var max = centerPos + _miningAreaRange;
            return targetPos.x >= min.x && targetPos.x <= max.x &&
                   targetPos.y >= min.y && targetPos.y <= max.y &&
                   targetPos.z >= min.z && targetPos.z <= max.z;
        }

        private int CalculateDamage()
        {
            // 採掘設定に基づいたダメージ計算
            return 1;
        }

        private void HandleDestroyedObject(IMapObject mapObject)
        {
            var items = _mapObjectFactory.GetMapObjectItems(mapObject.InstanceId);
            // インベントリへのアイテム追加処理を実装
            _mapObjectDatastore.Remove(mapObject.InstanceId);
        }
    }
}