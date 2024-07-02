using System;
using Client.Common;
using Core.Const;
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
        
        public ItemObjectData GetItemPrefab(string modId, string name)
        {
            foreach (var itemObject in itemObjects)
                if (itemObject.ModId == modId && itemObject.Name == name)
                    return itemObject;
            return null;
        }
    }
    
    [Serializable]
    public class ItemObjectData
    {
        [SerializeField] private string modId = AlphaMod.ModId;
        [SerializeField] private string name;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 rotation;
        public string ModId => modId;
        
        public string Name => name;
        
        public GameObject ItemPrefab => itemPrefab;
        
        public Vector3 Position => position;
        
        public Vector3 Rotation => rotation;
    }
}