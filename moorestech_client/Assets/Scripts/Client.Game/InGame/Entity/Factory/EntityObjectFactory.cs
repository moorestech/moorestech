using System;
using System.Collections.Generic;
using Client.Network.API;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// エンティティタイプに応じたIEntityObjectインスタンスを生成するファクトリー
    /// 各エンティティタイプ専用のファクトリーに処理を委譲する
    /// Factory to create IEntityObject instances according to entity type
    /// Delegates processing to factories specialized for each entity type
    /// </summary>
    public class EntityObjectFactory : MonoBehaviour
    {
        [SerializeField] private List<MonoBehaviour> entityFactories;
        
        private Dictionary<string, IEntityObjectFactory> _factoryMap;
        
        private void Awake()
        {
            // 各ファクトリーを型でマップに登録
            // Register each factory in map by type
            _factoryMap = new Dictionary<string, IEntityObjectFactory>();
            
            foreach (var factory in entityFactories)
            {
                if (factory is IEntityObjectFactory entityFactory)
                {
                    _factoryMap[entityFactory.SupportedEntityType] = entityFactory;
                }
                else
                {
                    Debug.LogWarning($"[EntityObjectFactory] {factory.GetType().Name} does not implement IEntityObjectFactory");
                }
            }
        }
        
        /// <summary>
        /// EntityResponseからエンティティタイプに応じたIEntityObjectを生成
        /// Create IEntityObject according to entity type from EntityResponse
        /// </summary>
        public IEntityObject CreateEntity(EntityResponse entity)
        {
            // エンティティタイプに対応するファクトリーを検索
            // Search for factory corresponding to entity type
            if (_factoryMap.TryGetValue(entity.Type, out var factory))
            {
                return factory.CreateEntity(entity);
            }
            
            throw new ArgumentException($"エンティティタイプに対応するファクトリーがありません: {entity.Type}");
        }
    }
}

