using System;
using System.Collections.Generic;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
using Client.Game.InGame.Train;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// エンティティタイプに応じたIEntityObjectインスタンスを生成するファクトリー
    /// 各エンティティタイプ専用のファクトリーに処理を委譲する
    /// Factory to create IEntityObject instances according to entity type
    /// Delegates processing to factories specialized for each entity type
    /// </summary>
    public class EntityObjectFactory
    {
        private readonly Dictionary<string, IEntityObjectFactory> _factoryMap;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        
        public EntityObjectFactory(TrainUnitClientCache trainUnitClientCache)
        {
            // 車両姿勢更新に必要な依存を保持する
            // Hold dependencies required for train car pose updates
            _trainUnitClientCache = trainUnitClientCache;
            _factoryMap = new Dictionary<string, IEntityObjectFactory>();
            _factoryMap.Add(VanillaEntityType.VanillaTrain, new TrainEntityObjectFactory(_trainUnitClientCache));
            _factoryMap.Add(VanillaEntityType.VanillaItem, new BeltConveyorItemEntityObjectFactory());
        }
        
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            if (_factoryMap.TryGetValue(entity.Type, out var factory))
            {
                return　await factory.CreateEntity(parent, entity);
            }
            
            throw new ArgumentException($"エンティティタイプに対応するファクトリーがありません: {entity.Type}");
        }
    }
}
