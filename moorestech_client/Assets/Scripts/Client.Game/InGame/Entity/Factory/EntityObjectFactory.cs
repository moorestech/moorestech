using System;
using System.Collections.Generic;
using Client.Network.API;
using Cysharp.Threading.Tasks;
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
        
        public EntityObjectFactory()
        {
            // ベルト上のアイテムはポーリングから外れた(複製から表示する)。ポーリングで表示するエンティティは今は無い
            // Belt items left the polling path (displayed from the replica); no entity kind is displayed through polling for now
            _factoryMap = new Dictionary<string, IEntityObjectFactory>();
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
