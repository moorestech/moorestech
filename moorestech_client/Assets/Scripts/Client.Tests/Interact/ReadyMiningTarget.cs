using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Interact
{
    // 常に採掘可能で、1フレームでは掘り終わらない対象
    // A target that is always mineable and never finishes within a single frame
    internal sealed class ReadyMiningTarget : IMiningTargetObject
    {
        public GameObject GameObject { get; } = new("ReadyMiningTarget");
        public bool IsInteractAvailable => true;
        public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
        public IReadOnlyList<Guid> EarnItemGuids => Array.Empty<Guid>();

        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
        {
            tool = new MiningToolCandidate(equippedItemId, 1f);
            recommendedToolItemIds = new List<ItemId>();
            return MiningStartOutcome.Ready;
        }

        public void SetHighlighted(bool highlighted)
        {
        }

        public void SendAttack()
        {
        }
    }
}
