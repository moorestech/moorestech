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

        // ツール不要の対象では推奨ツールが空になるため、毎回の確保を避けて共有する
        // Targets that need no tool return an empty recommendation, so share one instance instead of allocating
        private static readonly List<ItemId> EmptyToolItemIds = new();
        
        public bool IsDestroyed { get; private set; }
        public int CurrentHp { get; private set; }
        
        public int InstanceId => instanceId;
        public Guid MapObjectGuid => new(mapObjectGuid);
        public MapObjectMasterElement MapObjectMasterElement { get; private set; }
        public GameObject GameObject => gameObject;

        // マスタ欠損時は対象として扱わない
        // A master-less object is not a target
        public bool IsAvailable => !IsDestroyed && MapObjectMasterElement != null;

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

            // 個体スケールがUI表示に波及しないようHPバーは逆スケールで等倍を保つ（姿勢と同様、補正はView自身の責務）
            // Counter-scale the HP bar so per-instance scaling never distorts the UI (correction is the View's own responsibility, like its rotation)
            if (hpBarView) hpBarView.SetWorldUnitScale();
        }
        
        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
        {
            tool = default;
            recommendedToolItemIds = EmptyToolItemIds;

            if (!IsAvailable) return MiningStartOutcome.Unavailable;

            // PickUpはツールを介さず1操作で取得する
            // PickUp is acquired in a single action without any tool
            if (MapObjectMasterElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp) return MiningStartOutcome.InstantPickUp;

            var miningTools = ((MiningMiningParam)MapObjectMasterElement.MiningParam).MiningTools;
            if (!MapObjectMiningService.TryResolveUsableTool(equippedItemId, miningTools, out var usableTool))
            {
                recommendedToolItemIds = ToItemIds(miningTools);
                return MiningStartOutcome.ToolMismatch;
            }

            tool = new MiningToolCandidate(equippedItemId, usableTool.AttackSpeed);
            return MiningStartOutcome.Ready;

            #region Internal

            List<ItemId> ToItemIds(MiningToolsElement[] tools)
            {
                var itemIds = new List<ItemId>(tools.Length);
                foreach (var miningTool in tools)
                {
                    itemIds.Add(MasterHolder.ItemMaster.GetItemId(miningTool.ToolItemGuid));
                }

                return itemIds;
            }

            #endregion
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
