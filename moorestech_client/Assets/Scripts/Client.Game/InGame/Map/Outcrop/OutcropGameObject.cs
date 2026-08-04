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

            // 音種は露頭の見た目ではなく鉱脈マスタから決定する
            // Resolve the sound kind from vein master data rather than outcrop visuals
            _destroySoundType = element.SoundEffectType == MapVeinMasterElement.SoundEffectTypeConst.tree
                ? SoundEffectType.DestroyTree
                : SoundEffectType.DestroyStone;

            // 推奨表示と装備照合で共通のItemId表現を保持する
            // Retain the shared ItemId representation for recommendations and equipment matching
            foreach (var handMiningTool in _minableParam.HandMiningTools)
                UsableToolItemIds.Add(MasterHolder.ItemMaster.GetItemId(handMiningTool.ToolItemGuid));

            // プレハブ内の全コライダを同じ露頭ターゲットへ結び付ける
            // Bind every prefab collider to the same outcrop target
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

            // 装備GUIDが許可ツールに一致した場合だけ攻撃間隔を返す
            // Return the attack interval only when the equipped GUID matches an allowed tool
            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var handMiningTool in _minableParam.HandMiningTools)
            {
                if (handMiningTool.ToolItemGuid != equippedItemGuid) continue;
                tool = new MiningToolCandidate(equippedItemId, (float)handMiningTool.AttackSpeed);
                return true;
            }

            return false;
        }

        public void SetFocused(bool focused)
        {
            // 露頭のフォーカス演出はアート課題として後続対応する
            // Outcrop focus visuals remain a follow-up art task
        }

        public void SendAttack()
        {
            // AABB内座標をvein種別リクエストとしてサーバーへ送る
            // Send the in-AABB coordinate to the server as a vein request
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(
                ClientContext.PlayerConnectionSetting.PlayerId,
                _minePosition);
            ClientContext.VanillaApi.SendOnly.SendMiningRequest(request);
        }
    }
}
