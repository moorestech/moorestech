using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.NearestSearch;
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
    public class MapObjectGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget
    {
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private MapObjectHpBarView hpBarView;
        [SerializeField] private int instanceId;
        [SerializeField] private string mapObjectGuid;

        // ツール不要の対象では推奨ツールが空になるため、毎回の確保を避けて共有する
        // Targets that need no tool return an empty recommendation, so share one instance instead of allocating
        private static readonly List<ItemId> EmptyToolItemIds = new();

        public IReadOnlyList<Guid> EarnItemGuids { get; private set; } = Array.Empty<Guid>();
        public IReadOnlyList<ItemId> RecommendedToolItemIds { get; private set; } = EmptyToolItemIds;

        public bool IsDestroyed { get; private set; }
        public int CurrentHp { get; private set; }

        public int InstanceId => instanceId;

        // 最寄り探索が毎フレーム比較するため、文字列guidは注入時に1回だけパースして保持する
        // Nearest search compares this every frame, so parse the string guid once at injection and keep it
        public Guid MapObjectGuid { get; private set; }
        public MapObjectMasterElement MapObjectMasterElement { get; private set; }
        public GameObject GameObject => gameObject;

        // 指す先になれる条件は狙える条件と同じ。装飾物へピンを向けないためマスタ検証と実機挙動を揃える
        // Being pointable is the same condition as being aimable, so a pin never targets a decoration
        public bool IsSearchable => IsAvailable;

        // マスタ欠損・装飾物(None)は対象外
        // A master-less object or a decoration (None) is not a target
        public bool IsAvailable => !IsDestroyed && MapObjectMasterElement != null && !IsDecoration;

        private bool IsDecoration => MapObjectMaster.IsDecoration(MapObjectMasterElement);

        public Vector3 GetIndexPosition()
        {
            return transform.position;
        }

        public SoundEffectType DestroySoundType => MapObjectMiningPresentation.GetDestroySoundType(MapObjectMasterElement);
        
        public IObservable<Unit> OnDestroyMapObject => _onDestroyMapObject;
        private readonly Subject<Unit> _onDestroyMapObject = new();


        // 実行時Instantiate用にID/GUIDを注入する（ベイク時代のSerializeField直接参照の置換）
        // Injects identity for runtime instantiation (replaces baked SerializeField values)
        public void SetRuntimeIdentity(int instanceId, string mapObjectGuid)
        {
            this.instanceId = instanceId;
            this.mapObjectGuid = mapObjectGuid;
            MapObjectGuid = new Guid(mapObjectGuid);
        }

        public void Initialize(GetMapObjectInfoProtocol.MapObjectsInfoMessagePack mapObjectInfo)
        {
            CurrentHp = mapObjectInfo.CurrentHp;
            MapObjectMasterElement = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(MapObjectGuid);
            
            if (MapObjectMasterElement == null)
            {
                Debug.LogError($"MapObject GUID {MapObjectGuid} is not found");
                // マスタ欠損個体も狙えない。prefab既定の有効コライダーを残すと照準を遮る
                // A master-less object is not aimable either; leaving the prefab's enabled collider would block aiming
                ApplyRayTargetInteractable(false);
                return;
            }

            // 取得物と推奨ツールはマスタ確定時に1度だけ拾う
            // Resolve the yields and the recommended tools once the master is settled
            EarnItemGuids = MapObjectMiningPresentation.GetEarnItemGuids(MapObjectMasterElement);
            RecommendedToolItemIds = MapObjectMasterElement.MiningParam is MiningMiningParam miningToolParam ? ToItemIds(miningToolParam.MiningTools) : EmptyToolItemIds;


            // 装飾物と破壊済みはどちらも狙えない。可否を先に畳み、文の並び順へ依存させない
            // Neither a decoration nor a destroyed object is aimable; folding both first removes any dependence on statement order
            ApplyRayTargetInteractable(!IsDecoration && !mapObjectInfo.IsDestroyed);

            if (mapObjectInfo.IsDestroyed)
            {
                DestroyMapObject();
            }

            UpdateHpBar();

            // 個体スケールがUI表示に波及しないようHPバーは逆スケールで等倍を保つ（姿勢と同様、補正はView自身の責務）
            // Counter-scale the HP bar so per-instance scaling never distorts the UI (correction is the View's own responsibility, like its rotation)
            if (hpBarView) hpBarView.SetWorldUnitScale();
        }
        
        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            tool = default;

            if (!IsAvailable) return MiningStartOutcome.Unavailable;

            // PickUpはツールを介さず1操作で取得する
            // PickUp is acquired in a single action without any tool
            if (MapObjectMasterElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp) return MiningStartOutcome.InstantPickUp;

            // 採掘設定を持たない対象はここに来ない想定だが、型で守りダウンキャストの前提を並び順から外す
            // Targets without mining settings should not reach here, so guard by type instead of relying on statement order
            if (MapObjectMasterElement.MiningParam is not MiningMiningParam miningParam) return MiningStartOutcome.Unavailable;

            if (!MapObjectMiningService.TryResolveUsableTool(equippedItemId, miningParam.MiningTools, out var usableTool)) return MiningStartOutcome.ToolMismatch;

            tool = new MiningToolCandidate(equippedItemId, usableTool.AttackSpeed);
            return MiningStartOutcome.Ready;
        }

        // 推奨ツールはマスタ由来で不変なのでマスタ確定時に1度だけ写す
        // The recommended tools come from the master and never change, so they are copied once the master is settled
        private static List<ItemId> ToItemIds(MiningToolsElement[] tools)
        {
            var itemIds = new List<ItemId>(tools.Length);
            foreach (var miningTool in tools)
            {
                itemIds.Add(MasterHolder.ItemMaster.GetItemId(miningTool.ToolItemGuid));
            }

            return itemIds;
        }

        // 採掘の可用性と同じ条件で候補になる
        // Becomes a candidate under the same condition as mining availability
        public bool IsInteractAvailable => IsAvailable;

        public void SetHighlighted(bool highlighted)
        {
            if (outlineObject) outlineObject.SetActive(highlighted);
            if (hpBarView) hpBarView.SetActive(highlighted);
        }

        public void SendAttack()
        {
            ClientContext.VanillaApi.SendOnly.AttackMapObject(InstanceId);
        }
        
        // 開幕スキットの非活性窓で生成される近傍個体があるため、非活性の子も走査する（2026-08-23裁定）
        // Near-field objects can be born inside the opening skit's inactive window, so inactive children are scanned too (adjudicated 2026-08-23)
        private void ApplyRayTargetInteractable(bool interactable)
        {
            foreach (var rayTarget in GetComponentsInChildren<MapObjectRayTarget>(true))
            {
                rayTarget.Initialize(this, interactable);
            }
        }

        public void DestroyMapObject()
        {
            IsDestroyed = true;
            //自分を含む全ての子のコライダーとレンダラーを無効化する。非活性下で生成された個体も確実に落とす
            // Disable colliders and renderers on self and every child, including ones born while inactive
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                var collider = child.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
            
            _onDestroyMapObject.OnNext(Unit.Default);
        }
        
        public void UpdateHp(int newHp)
        {
            CurrentHp = newHp;
            UpdateHpBar();
        }
        
        private void UpdateHpBar()
        {
            if (!hpBarView) return;

            // 装飾物はHPを持たず、削れないのでバーも出さない
            // A decoration has no HP and is never worn down, so no bar is shown
            if (MapObjectMasterElement.MiningParam is not IMinableMapObjectParam minableParam) return;

            hpBarView.SetHp(CurrentHp, minableParam.Hp);
        }
    }
}
