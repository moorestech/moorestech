using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using Game.Map;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     鉱脈AABBごとに生成されるクライアント専用の手掘り対象
    ///     Client-only hand-mining target instantiated for each vein AABB
    /// </summary>
    public class OutcropGameObject : MonoBehaviour, IMiningTargetObject
    {
        private static readonly HandMiningToolsElement[] NoHandMiningTools = Array.Empty<HandMiningToolsElement>();

        private HandMiningToolsElement[] _handMiningTools = NoHandMiningTools;
        private Guid _veinGuid;
        private Vector3Int _minePosition;

        public GameObject GameObject => gameObject;
        public bool IsAvailable => true;
        public bool CanHandMine { get; private set; }
        public bool IsPickUp => false;
        public List<ItemId> UsableToolItemIds { get; } = new();
        public SoundEffectType DestroySoundType { get; private set; }

        public void Initialize(MapVeinMasterElement element, Guid veinGuid, Vector3Int minePosition)
        {
            _veinGuid = veinGuid;
            _minePosition = minePosition;

            // 掘削機専用の鉱脈も露頭は立つので、可否だけ落として初期化そのものは最後まで通す
            // A drill-only vein still gets an outcrop, so only the permission drops while initialization runs to the end
            var minableParam = element.HandMiningParam as MinableHandMiningParam;
            CanHandMine = minableParam != null;
            _handMiningTools = CanHandMine ? minableParam.HandMiningTools : NoHandMiningTools;

            // 音種は鉱脈マスタ準拠
            // Resolve sound from vein master
            DestroySoundType = element.SoundEffectType == MapVeinMasterElement.SoundEffectTypeConst.tree
                ? SoundEffectType.DestroyTree
                : SoundEffectType.DestroyStone;

            foreach (var handMiningTool in _handMiningTools)
                UsableToolItemIds.Add(MasterHolder.ItemMaster.GetItemId(handMiningTool.ToolItemGuid));

            // 掘れない露頭もレイを吸わせるため、全Colliderを可否に関わらず露頭へ紐付ける
            // Bind every collider regardless of permission so an unmineable outcrop still absorbs the ray
            foreach (var childCollider in GetComponentsInChildren<Collider>(true))
            {
                var rayTarget = childCollider.GetComponent<OutcropRayTarget>();
                if (rayTarget == null) rayTarget = childCollider.gameObject.AddComponent<OutcropRayTarget>();
                rayTarget.Initialize(this);
            }
        }

        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            // 権威判定と同じ照合を使い、クライアントの見た目とサーバーの可否がずれないようにする
            // Reuse the authority's matching so the client's view never diverges from the server's verdict
            if (VeinHandMiningService.TryResolveUsableTool(equippedItemId, _handMiningTools, out var usableTool))
            {
                tool = new MiningToolCandidate(equippedItemId, usableTool.AttackSpeed);
                return true;
            }

            tool = default;
            return false;
        }

        public void SetFocused(bool focused)
        {
            // 露頭演出は後続対応
            // Outcrop visuals follow later
        }

        public void SendAttack()
        {
            ClientContext.VanillaApi.SendOnly.MineVein(_veinGuid, _minePosition);
        }
    }
}
