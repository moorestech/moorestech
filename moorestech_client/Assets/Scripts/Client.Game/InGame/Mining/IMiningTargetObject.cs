using System.Collections.Generic;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     手掘り開始要求の結果
    ///     Outcome of a hand-mining start request
    /// </summary>
    public enum MiningStartOutcome
    {
        // 進捗つきで掘り始められる
        // Progress mining can start
        Ready,

        // 進捗を挟まず1操作で取得する
        // Acquired in a single action without progress
        InstantPickUp,

        // 破壊済み・マスタ欠損などで採掘対象そのものでない
        // Not a mining target at all, e.g. destroyed or missing master
        Unavailable,

        // 対象自体が手掘りを許していない
        // The target itself forbids hand mining
        HandMiningNotAllowed,

        // 装備が対象の許可ツールに一致しない
        // The equipment matches none of the target's allowed tools
        ToolMismatch,
    }

    /// <summary>
    ///     手掘りFSMが扱う採掘対象の抽象
    ///     Abstraction of a hand-mining target handled by the mining FSM
    /// </summary>
    public interface IMiningTargetObject
    {
        GameObject GameObject { get; }
        SoundEffectType DestroySoundType { get; }

        // 可否・種別・ツール解決を1回の問い合わせへ畳み、成立しない組み合わせを呼び出し側に作らせない
        // Fold availability, kind and tool resolution into one query so callers cannot build impossible combinations
        MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds);

        void SetFocused(bool focused);

        // ダメージ算出はサーバ権威のため、打撃対象だけを送る
        // Damage is computed by the server authority, so only the struck target is sent
        void SendAttack();
    }
}
