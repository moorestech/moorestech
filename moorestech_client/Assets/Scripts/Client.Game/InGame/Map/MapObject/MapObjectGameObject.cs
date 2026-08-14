using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using Game.Map;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     MapObjectのGameObjectを表すクラス
    ///     TODO 今はUnity上に直接おいているので、今後はちゃんとサーバーからデータを受け取って生成するようにする
    /// </summary>
    public class MapObjectGameObject : MonoBehaviour, IMiningTargetObject
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
        public GameObject GameObject => gameObject;
        // マスタ欠損時はInitializeが途中で戻るため、対象として生きていないものとして扱う
        // Initialize returns early when the master is missing, so treat such an object as not live
        public bool IsAvailable => !IsDestroyed && MapObjectMasterElement != null;
        public bool CanHandMine => true;
        public bool IsPickUp => MapObjectMasterElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp;

        public List<ItemId> UsableToolItemIds
        {
            get
            {
                var miningTools = ((MiningMiningParam)MapObjectMasterElement.MiningParam).MiningTools;
                var itemIds = new List<ItemId>(miningTools.Length);

                // ツールGUIDをItemId化
                // Convert tool GUIDs to ItemIds
                foreach (var miningTool in miningTools)
                {
                    itemIds.Add(MasterHolder.ItemMaster.GetItemId(miningTool.ToolItemGuid));
                }

                return itemIds;
            }
        }

        public SoundEffectType DestroySoundType
        {
            get
            {
                switch (MapObjectMasterElement.SoundEffectType)
                {
                    case MapObjectMasterElement.SoundEffectTypeConst.stone:
                        return SoundEffectType.DestroyStone;
                    case MapObjectMasterElement.SoundEffectTypeConst.tree:
                        return SoundEffectType.DestroyTree;
                    default:
                        Debug.LogError("採掘音が設定されていません");
                        return SoundEffectType.DestroyStone;
                }
            }
        }
        
        public IObservable<Unit> OnDestroyMapObject => _onDestroyMapObject;
        private readonly Subject<Unit> _onDestroyMapObject = new();


        // 実行時Instantiate用にID/GUIDを注入する（ベイク時代のSerializeField直接参照の置換）
        // Injects identity for runtime instantiation (replaces baked SerializeField values)
        public void SetRuntimeIdentity(int instanceId, string mapObjectGuid)
        {
            this.instanceId = instanceId;
            this.mapObjectGuid = mapObjectGuid;
        }

        public void Initialize(GetMapObjectInfoProtocol.MapObjectsInfoMessagePack mapObjectInfo)
        {
            CurrentHp = mapObjectInfo.CurrentHp;
            MapObjectMasterElement = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(MapObjectGuid);
            
            if (MapObjectMasterElement == null)
            {
                Debug.LogError($"MapObject GUID {MapObjectGuid} is not found");
                return;
            }
            
            if (mapObjectInfo.IsDestroyed)
            {
                DestroyMapObject();
            }
            
            UpdateHpBar();
            
            var rayTargets = GetComponentsInChildren<MapObjectRayTarget>();
            foreach (var rayTarget in rayTargets)
            {
                rayTarget.Initialize(this);
            }
        }
        
        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            var miningTools = ((MiningMiningParam)MapObjectMasterElement.MiningParam).MiningTools;
            if (MapObjectMiningService.TryResolveUsableTool(equippedItemId, miningTools, out var usableTool))
            {
                tool = new MiningToolCandidate(equippedItemId, usableTool.AttackSpeed);
                return true;
            }

            tool = default;
            return false;
        }

        public void SetFocused(bool focused)
        {
            if (outlineObject) outlineObject.SetActive(focused);
            if (hpBarView) hpBarView.SetActive(focused);
        }

        public void SendAttack()
        {
            ClientContext.VanillaApi.SendOnly.AttackMapObject(InstanceId);
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
    }
}
