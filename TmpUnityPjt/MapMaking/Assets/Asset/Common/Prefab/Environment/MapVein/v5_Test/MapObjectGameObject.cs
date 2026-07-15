using System;

using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     MapObjectのGameObjectを表すクラス
    ///     TODO 今はUnity上に直接おいているので、今後はちゃんとサーバーからデータを受け取って生成するようにする
    /// </summary>
    public class MapObjectGameObject : MonoBehaviour
    {
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private MapObjectHpBarView hpBarView;
        [SerializeField] private int instanceId;
        [SerializeField] private string mapObjectGuid;

    }
}