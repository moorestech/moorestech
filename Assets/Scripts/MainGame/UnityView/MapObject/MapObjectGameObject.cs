using UnityEngine;

namespace MainGame.UnityView.MapObject
{
    /// <summary>
    /// MapObjectのGameObjectを表すクラス
    /// TODO 今はUnity上に直接おいているので、今後はちゃんとサーバーからデータを受け取って生成するようにする
    /// </summary>
    public class MapObjectGameObject : MonoBehaviour
    {
        [SerializeField] private int instanceId;
        public int InstanceId => instanceId;

        [SerializeField] private string mapObjectType;
        public string MapObjectType => mapObjectType;
    }
}