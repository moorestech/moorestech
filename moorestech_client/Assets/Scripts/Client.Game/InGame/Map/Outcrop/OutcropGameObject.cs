using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.NearestSearch;
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
    public class OutcropGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget
    {
        private static readonly HandMiningToolsElement[] NoHandMiningTools = Array.Empty<HandMiningToolsElement>();

        private HandMiningToolsElement[] _handMiningTools = NoHandMiningTools;
        private bool _handMiningAllowed;
        private Guid _veinGuid;
        private Vector3Int _minePosition;

        private readonly List<ItemId> _usableToolItemIds = new();

        public GameObject GameObject => gameObject;
        public Vector3 Position => transform.position;
        public SoundEffectType DestroySoundType { get; private set; }

        public void Initialize(MapVeinMasterElement element, Guid veinGuid, Vector3Int minePosition)
        {
            _veinGuid = veinGuid;
            _minePosition = minePosition;

            // 不可の鉱脈も初期化は最後まで通す
            // Even an unmineable vein initializes fully
            var minableParam = element.HandMiningParam as MinableHandMiningParam;
            _handMiningAllowed = minableParam != null;
            _handMiningTools = _handMiningAllowed ? minableParam.HandMiningTools : NoHandMiningTools;

            // 音種は鉱脈マスタ準拠
            // Resolve sound from vein master
            DestroySoundType = element.SoundEffectType == MapVeinMasterElement.SoundEffectTypeConst.tree
                ? SoundEffectType.DestroyTree
                : SoundEffectType.DestroyStone;

            _usableToolItemIds.Clear();
            foreach (var handMiningTool in _handMiningTools)
                _usableToolItemIds.Add(MasterHolder.ItemMaster.GetItemId(handMiningTool.ToolItemGuid));

            // 掘れない露頭もレイを吸わせる
            // An unmineable outcrop absorbs the ray too
            foreach (var childCollider in GetComponentsInChildren<Collider>(true))
            {
                var rayTarget = childCollider.GetComponent<OutcropRayTarget>();
                if (rayTarget == null) rayTarget = childCollider.gameObject.AddComponent<OutcropRayTarget>();
                rayTarget.Initialize(this);
            }
        }

        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
        {
            tool = default;
            recommendedToolItemIds = _usableToolItemIds;

            // 露頭は無限資源なので消滅せず、手掘り不可だけが不成立の理由になる
            // An outcrop never disappears, so the only refusal is a vein that forbids hand mining
            if (!_handMiningAllowed) return MiningStartOutcome.HandMiningNotAllowed;

            // 権威判定と同じ照合を使う
            // Reuse the authority's own matching
            if (!VeinHandMiningService.TryResolveUsableTool(equippedItemId, _handMiningTools, out var usableTool))
                return MiningStartOutcome.ToolMismatch;

            tool = new MiningToolCandidate(equippedItemId, usableTool.AttackSpeed);
            return MiningStartOutcome.Ready;
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
