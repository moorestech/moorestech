using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.MapObject
{
    /// <summary>
    /// TODO 静的なオブジェクトになってるので、サーバーからコンフィグを取得して動的に生成するようにしたい
    /// </summary>
    public class MapObjectGameObjectController : MonoBehaviour
    {
        [SerializeField] private List<MapObjectGameObject> stoneMapObjects;
    }
}