using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     鉱脈AABBごとに生成されるクライアント専用の手掘り対象
    ///     Client-only hand-mining target instantiated for each vein AABB
    /// </summary>
    public class OutcropGameObject : MonoBehaviour, IMiningTargetObject
    {
        private MinableHandMiningParam _minableParam;
        private Vector3Int _minePosition;
        private SoundEffectType _destroySoundType;

        public GameObject GameObject => gameObject;
        public bool IsAvailable => true;
        public bool IsPickUp => false;
        public List<ItemId> UsableToolItemIds { get; } = new();
        public SoundEffectType DestroySoundType => _destroySoundType;

        public void Initialize(MapVeinMasterElement element, Vector3Int minePosition)
        {
            _minableParam = (MinableHandMiningParam)element.HandMiningParam;
            _minePosition = minePosition;

            // 音種は鉱脈マスタ準拠
            // Resolve sound from vein master
            _destroySoundType = element.SoundEffectType == MapVeinMasterElement.SoundEffectTypeConst.tree
                ? SoundEffectType.DestroyTree
                : SoundEffectType.DestroyStone;

            // ツールIDを共通化
            // Share tool IDs
            foreach (var handMiningTool in _minableParam.HandMiningTools)
                UsableToolItemIds.Add(MasterHolder.ItemMaster.GetItemId(handMiningTool.ToolItemGuid));

            // 全Colliderを露頭へ紐付け
            // Bind all colliders to outcrop
            foreach (var childCollider in GetComponentsInChildren<Collider>(true))
            {
                var rayTarget = childCollider.GetComponent<OutcropRayTarget>();
                if (rayTarget == null) rayTarget = childCollider.gameObject.AddComponent<OutcropRayTarget>();
                rayTarget.Initialize(this);
            }
        }

        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            tool = default;
            if (equippedItemId == ItemMaster.EmptyItemId) return false;

            // 許可ツールの間隔を返す
            // Return allowed tool interval
            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var handMiningTool in _minableParam.HandMiningTools)
            {
                if (handMiningTool.ToolItemGuid != equippedItemGuid) continue;
                tool = new MiningToolCandidate(equippedItemId, handMiningTool.AttackSpeed);
                return true;
            }

            return false;
        }

        public void SetFocused(bool focused)
        {
            // 露頭演出は後続対応
            // Outcrop visuals follow later
        }

        public void SendAttack()
        {
            // AABB内座標で採掘送信
            // Send mining with in-AABB position
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(
                ClientContext.PlayerConnectionSetting.PlayerId,
                _minePosition);
            ClientContext.VanillaApi.SendOnly.SendMiningRequest(request);
        }
    }
}
