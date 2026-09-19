using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Interact
{
    // 装備が合わず掘れない採掘対象
    // A mining target the current equipment cannot mine
    internal sealed class ToolMismatchMiningTarget : IMiningTargetObject
    {
        public GameObject GameObject { get; } = new("ToolMismatchMiningTarget");
        public bool IsInteractAvailable => true;
        public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
        public IReadOnlyList<Guid> EarnItemGuids => Array.Empty<Guid>();
        public IReadOnlyList<ItemId> RecommendedToolItemIds => Array.Empty<ItemId>();
        public int SendAttackCount { get; private set; }

        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            tool = new MiningToolCandidate(equippedItemId, 1f);
            return MiningStartOutcome.ToolMismatch;
        }

        public void SetHighlighted(bool highlighted)
        {
        }

        public void SendAttack()
        {
            SendAttackCount++;
        }
    }
}
