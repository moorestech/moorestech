using System;
using Client.Common;
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

        public ItemObjectData GetItemPrefab(string modId, string name)
        {
            foreach (var itemObject in itemObjects)
            {
                if (itemObject.ModId == modId && itemObject.Name == name)
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
        public string ModId => modId;
        [SerializeField] private string modId = AlphaMod.ModId;

        public string Name => name;
        [SerializeField] private string name;

        public GameObject ItemPrefab => itemPrefab;
        [SerializeField] private GameObject itemPrefab;

        public Vector3 Position => position;
        [SerializeField] private Vector3 position;

        public Vector3 Rotation => rotation;
        [SerializeField] private Vector3 rotation;
    }
}