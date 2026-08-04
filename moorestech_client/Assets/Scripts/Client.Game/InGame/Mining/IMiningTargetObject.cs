using System.Collections.Generic;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     手掘りFSMが扱う採掘対象の抽象
    ///     Abstraction of a hand-mining target handled by the mining FSM
    /// </summary>
    public interface IMiningTargetObject
    {
        GameObject GameObject { get; }
        bool IsAvailable { get; }
        bool IsPickUp { get; }
        List<ItemId> UsableToolItemIds { get; }
        SoundEffectType DestroySoundType { get; }

        bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool);
        void SetFocused(bool focused);
        void SendAttack();
    }
}
