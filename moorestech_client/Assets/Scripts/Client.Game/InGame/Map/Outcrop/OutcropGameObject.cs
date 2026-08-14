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

            // 不可の鉱脈も初期化は最後まで通す
            // Even an unmineable vein initializes fully
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

            // 掘れない露頭もレイを吸わせる
            // An unmineable outcrop absorbs the ray too
            foreach (var childCollider in GetComponentsInChildren<Collider>(true))
            {
                var rayTarget = childCollider.GetComponent<OutcropRayTarget>();
                if (rayTarget == null) rayTarget = childCollider.gameObject.AddComponent<OutcropRayTarget>();
                rayTarget.Initialize(this);
            }
        }

        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            // 権威判定と同じ照合を使う
            // Reuse the authority's own matching
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
