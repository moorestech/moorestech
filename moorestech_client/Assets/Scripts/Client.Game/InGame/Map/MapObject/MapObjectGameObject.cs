using System;
using Core.Master;
using Mooresmaster.Model.MapObjectsModule;
using UniRx;
using UnityEditor;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     MapObjectのGameObjectを表すクラス
    ///     TODO 今はUnity上に直接おいているので、今後はちゃんとサーバーからデータを受け取って生成するようにする
    /// </summary>
    public class MapObjectGameObject : MonoBehaviour
    {
        public int InstanceId => instanceId;
        public Guid MapObjectGuid => new(mapObjectGuid);
        public MapObjectMasterElement MapObjectMasterElement => MasterHolder.MapObjectMaster.GetMapObjectElement(MapObjectGuid);
        
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private int instanceId;
        [SerializeField] private string mapObjectGuid;
        
        public bool IsDestroyed { get; private set; }
        
        public IObservable<Unit> OnDestroyMapObject => _onDestroyMapObject;
        private readonly Subject<Unit> _onDestroyMapObject = new();
        
        public void OutlineEnable(bool enable)
        {
            if (outlineObject != null)
            {
                outlineObject.SetActive(enable);
            }
        }
        
        public void DestroyMapObject()
        {
            IsDestroyed = true;
            //自分を含む全ての子のコライダーとレンダラーを無効化する
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                var collider = child.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
            
            _onDestroyMapObject.OnNext(Unit.Default);
        }
        
        public Vector3 GetPosition()
        {
            return transform.position;
        }
        
#if UNITY_EDITOR
        public void SetMapObjectData(int instanceId)
        {
            Undo.RecordObject(this, "SetMapObjectData");
            
            this.instanceId = instanceId;
            
            //Dirtyフラグを立てる
            EditorUtility.SetDirty(this);
        }
        
#endif
    }
}