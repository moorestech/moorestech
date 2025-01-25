using System;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Define
{
    /// <summary>
    ///     TODO このコードはalpha2.0以降で消す
    /// </summary>
    [Obsolete("Alpha2.0用のコンテナ")]
    [CreateAssetMenu(fileName = "ItemObjectContainer", menuName = "moorestech/ItemObjectContainer", order = 0)]
    public class ItemObjectContainer : ScriptableObject
    {
        [SerializeField] private ItemObjectData[] itemObjects;
        
        public ItemObjectData GetItemPrefab(ItemId itemId)
        {
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(itemId).ItemGuid;
            foreach (var itemObject in itemObjects)
            {
                if (itemObject.ItemGuid == itemGuid)
                {
                    return itemObject;
                }
            }
            
            return null;
        }
    }
    
    [Serializable]
    public class ItemObjectData
    {
        public Guid ItemGuid => Guid.Parse(itemGuid);
        public GameObject ItemPrefab => itemPrefab;
        public Vector3 Position => position;
        public Vector3 Rotation => rotation;
        
        [SerializeField] private string itemGuid;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 rotation;
    }
}