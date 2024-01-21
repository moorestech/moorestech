using System;
using Constant;
using UnityEngine;

namespace MainGame.UnityView.Item
{
    /// <summary>
    /// TODO このコードはalpha2.0以降で消す
    /// </summary>
    [Obsolete("Alpha2.0用のコンテナ")]
    [CreateAssetMenu(fileName = "ItemObjectContainer", menuName = "ItemObjectContainer", order = 0)]
    public class ItemObjectContainer : ScriptableObject
    {
        [SerializeField] private ItemObjectData[] itemObjects;

        public GameObject GetItemPrefab(string modId, string name)
        {
            foreach (var itemObject in itemObjects)
            {
                if (itemObject.ModId == modId && itemObject.Name == name)
                {
                    return itemObject.ItemPrefab;
                }
            }

            Debug.LogError("アイテムが見つかりませんでした。" + modId + ":" + name);
            return null;
        }
    }

    [Serializable]
    public class ItemObjectData
    {
        public string ModId => modId;
        [SerializeField] private string modId = AlphaMod.ModId;
        
        public string Name => name;
        [SerializeField] private string name;
        
        public GameObject ItemPrefab => itemPrefab;
        [SerializeField] private GameObject itemPrefab;
    }
}