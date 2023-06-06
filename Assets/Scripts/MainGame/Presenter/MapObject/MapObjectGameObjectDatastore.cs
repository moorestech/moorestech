using System.Collections.Generic;
using MainGame.UnityView.MapObject;
using UnityEngine;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// TODO 静的なオブジェクトになってるので、サーバーからコンフィグを取得して動的に生成するようにしたい
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour
    {
        [SerializeField] private List<MapObjectGameObject> stoneMapObjects;
        
        
        
    }
}