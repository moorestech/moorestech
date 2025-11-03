using System;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using Game.Entity.Interface;
using UnityEngine;

namespace Client.Game.InGame.Entity
{
    /// <summary>
    /// エンティティタイプに応じたIEntityObjectインスタンスを生成するファクトリー
    /// Factory to create IEntityObject instances according to entity type
    /// </summary>
    public class EntityObjectFactory : MonoBehaviour
    {
        [SerializeField] private ItemEntityObject itemPrefab;
        [SerializeField] private GameObject defaultTrainPrefab;
        
        /// <summary>
        /// EntityResponseからエンティティタイプに応じたIEntityObjectを生成
        /// Create IEntityObject according to entity type from EntityResponse
        /// </summary>
        public IEntityObject CreateEntity(EntityResponse entity)
        {
            // アイテムエンティティの処理
            // Process item entity
            if (entity.Type == VanillaEntityType.VanillaItem)
            {
                return CreateItemEntity(entity);
            }
            
            // 列車エンティティの処理
            // Process train entity
            if (entity.Type == VanillaEntityType.VanillaTrain)
            {
                return CreateTrainEntity(entity);
            }
            
            throw new ArgumentException("エンティティタイプがありません");
        }
        
        #region Internal
        
        /// <summary>
        /// アイテムエンティティを生成
        /// Create item entity
        /// </summary>
        private IEntityObject CreateItemEntity(EntityResponse entity)
        {
            var item = Instantiate(itemPrefab, entity.Position, Quaternion.identity, transform);
            
            var id = new ItemId(int.Parse(entity.State.Split(',')[0]));
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            Texture texture = null;
            if (viewData != null)
            {
                texture = viewData.ItemTexture;
            }
            
            item.SetTexture(texture);
            return item;
        }
        
        /// <summary>
        /// 列車エンティティを生成する
        /// State文字列からTrainIdを取得し、Prefabをロードして表示
        /// Create train entity
        /// Get TrainId from State string, load Prefab and display
        /// </summary>
        private IEntityObject CreateTrainEntity(EntityResponse entity)
        {
            // State文字列からTrainIdをパース
            // Parse TrainId from State string
            if (!Guid.TryParse(entity.State, out var trainId))
            {
                Debug.LogError($"[EntityObjectFactory] Failed to parse TrainId from State: {entity.State}");
                return CreateFallbackTrainEntity(entity.Position);
            }
            
            // マスターデータからaddressablePathを取得
            // Get addressablePath from master data
            var trainMaster = MasterHolder.TrainUnitMaster;
            if (trainMaster?.Train?.TrainUnits == null)
            {
                Debug.LogWarning($"[EntityObjectFactory] TrainMaster is not initialized");
                return CreateFallbackTrainEntity(entity.Position);
            }
            
            // TrainIdに対応するTrainUnitMasterElementを検索
            // Search for TrainUnitMasterElement corresponding to TrainId
            var trainUnitElement = FindTrainUnitByItemGuid(trainMaster, trainId);
            if (trainUnitElement == null || string.IsNullOrEmpty(trainUnitElement.AddressablePath))
            {
                Debug.LogWarning($"[EntityObjectFactory] Train master data not found or addressablePath is empty for TrainId: {trainId}");
                return CreateFallbackTrainEntity(entity.Position);
            }
            
            // TODO: Addressablesで非同期ロード（後で実装）
            // For now, use fallback prefab
            // TODO: Load asynchronously with Addressables (implement later)
            // For now, use fallback prefab
            Debug.LogWarning($"[EntityObjectFactory] Addressables loading not implemented yet. Using fallback for: {trainUnitElement.AddressablePath}");
            return CreateFallbackTrainEntity(entity.Position);
        }
        
        /// <summary>
        /// TrainIdからTrainUnitMasterElementを検索
        /// TrainUnitのitemGuidとTrainIdを比較して検索（簡易実装）
        /// Search TrainUnitMasterElement from TrainId
        /// Search by comparing TrainUnit's itemGuid with TrainId (simplified implementation)
        /// </summary>
        private Mooresmaster.Model.TrainModule.TrainUnitMasterElement FindTrainUnitByItemGuid(TrainUnitMaster trainMaster, Guid trainId)
        {
            foreach (var unit in trainMaster.Train.TrainUnits)
            {
                // itemGuidとtrainIdの比較（簡易実装）
                // Compare itemGuid with trainId (simplified implementation)
                if (unit.ItemGuid.HasValue && unit.ItemGuid.Value == trainId)
                {
                    return unit;
                }
            }
            return null;
        }
        
        /// <summary>
        /// フォールバック用のデフォルト列車Prefabを生成
        /// Prefabロード失敗時やaddressablePath未設定時に使用
        /// Create default train Prefab for fallback
        /// Used when Prefab loading fails or addressablePath is not set
        /// </summary>
        private IEntityObject CreateFallbackTrainEntity(Vector3 position)
        {
            GameObject trainObject;
            
            // デフォルトPrefabが設定されていればそれを使用、なければシンプルなキューブを生成
            // Use default Prefab if set, otherwise create simple cube
            if (defaultTrainPrefab != null)
            {
                trainObject = Instantiate(defaultTrainPrefab, position, Quaternion.identity, transform);
            }
            else
            {
                // デフォルトPrefabが未設定の場合は、Unityのプリミティブキューブを生成
                // If default Prefab is not set, create Unity's primitive cube
                trainObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trainObject.transform.position = position;
                trainObject.transform.SetParent(transform);
            }
            
            // TrainEntityObjectコンポーネントをアタッチ
            // Attach TrainEntityObject component
            var trainEntityObject = trainObject.AddComponent<TrainEntityObject>();
            return trainEntityObject;
        }
        
        #endregion
    }
}

