using UnityEditor;
using UnityEngine;

namespace MainGame.UnityView.MapObject
{
    /// <summary>
    ///     MapObjectのGameObjectを表すクラス
    ///     TODO 今はUnity上に直接おいているので、今後はちゃんとサーバーからデータを受け取って生成するようにする
    /// </summary>
    public class MapObjectGameObject : MonoBehaviour
    {
        [SerializeField] private GameObject outlineObject;

        [SerializeField] private int instanceId;

        [SerializeField] private string mapObjectType;
        public int InstanceId => instanceId;
        public string MapObjectType => mapObjectType;


        public void OutlineEnable(bool enable)
        {
            outlineObject.SetActive(enable);
        }

        public void DestroyMapObject()
        {
            //自分を含む全ての子のコライダーとレンダラーを無効化する
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                var collider = child.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }


#if UNITY_EDITOR
        public void SetMapObjectData(int instanceId)
        {
            Undo.RecordObject(this, "SetMapObjectData");

            this.instanceId = instanceId;
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }
#endif
    }
}