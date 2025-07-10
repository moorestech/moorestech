using System;
using Core.Master;
using Mooresmaster.Model.MapObjectsModule;
using Server.Protocol.PacketResponse;
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
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private MapObjectHpBarView hpBarView;
        [SerializeField] private int instanceId;
        [SerializeField] private string mapObjectGuid;
        
        public bool IsDestroyed { get; private set; }
        public int CurrentHp { get; private set; }
        
        public int InstanceId => instanceId;
        public Guid MapObjectGuid => new(mapObjectGuid);
        public MapObjectMasterElement MapObjectMasterElement { get; private set; }
        
        public IObservable<Unit> OnDestroyMapObject => _onDestroyMapObject;
        private readonly Subject<Unit> _onDestroyMapObject = new();
        
        
        public void Initialize(GetMapObjectInfoProtocol.MapObjectsInfoMessagePack mapObjectInfo)
        {
            if (mapObjectInfo.IsDestroyed)
            {
                DestroyMapObject();
            }
            
            CurrentHp = mapObjectInfo.CurrentHp;
            MapObjectMasterElement = MasterHolder.MapObjectMaster.GetMapObjectElement(MapObjectGuid);
            UpdateHpBar();
            
            var rayTargets = GetComponentsInChildren<MapObjectRayTarget>();
            foreach (var rayTarget in rayTargets)
            {
                rayTarget.Initialize(this);
            }
        }
        
        public void OnFocus(bool isFocused)
        {
            if (outlineObject)
            {
                outlineObject.SetActive(isFocused);
            }
            if (hpBarView)
            {
                hpBarView.SetActive(isFocused);
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
        
        public void UpdateHp(int newHp)
        {
            CurrentHp = newHp;
            UpdateHpBar();
        }
        
        private void UpdateHpBar()
        {
            if (hpBarView)
            {
                hpBarView.SetHp(CurrentHp, MapObjectMasterElement.Hp);
            }
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