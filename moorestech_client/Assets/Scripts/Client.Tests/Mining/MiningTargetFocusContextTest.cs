using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining
{
    public class MiningTargetFocusContextTest
    {
        private static readonly Guid FirstEarnItemGuid = new("00000000-0000-0000-9999-000000000001");
        private static readonly Guid SecondEarnItemGuid = new("00000000-0000-0000-9999-000000000002");

        [Test]
        public void SetFocusTargetは同一対象を再設定しない()
        {
            var context = new MiningControllerContext(null);
            var sharedGameObject = new GameObject("SharedTarget");
            var firstTarget = new StubMiningTarget(sharedGameObject, Array.Empty<Guid>());
            var secondTarget = new StubMiningTarget(new GameObject("Second"), Array.Empty<Guid>());

            context.SetFocusTarget(firstTarget);
            Assert.AreEqual(1, firstTarget.EarnItemGuidsAccessCount);

            // 同一対象への再設定はEarnItemGuidsを再解決しない（アクセス回数が増えないことで検証する）
            // Re-setting the same target must not re-resolve EarnItemGuids (verified by the access count staying flat)
            context.SetFocusTarget(firstTarget);
            Assert.AreEqual(1, firstTarget.EarnItemGuidsAccessCount);
            Assert.AreSame(firstTarget, context.CurrentFocusTarget);

            context.SetFocusTarget(secondTarget);
            Assert.AreEqual(1, secondTarget.EarnItemGuidsAccessCount);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);

            context.SetFocusTarget(null);
            Assert.IsNull(context.CurrentFocusTarget);

            UnityEngine.Object.DestroyImmediate(sharedGameObject);
            UnityEngine.Object.DestroyImmediate(secondTarget.GameObject);
        }

        [Test]
        public void 取得アイテム名はフォーカス変化時に組み立てて保持される()
        {
            // 実辞書を通す。未登録キーは[!key]
            // Resolve through the real dictionary; unknown keys fall back to [!key], which is enough here
            Localize.Initialize();

            var context = new MiningControllerContext(null);
            var twoItemObject = new GameObject("TwoItemTarget");
            var noItemObject = new GameObject("NoItemTarget");
            var twoItemTarget = new StubMiningTarget(twoItemObject, new[] { FirstEarnItemGuid, SecondEarnItemGuid });
            var noItemTarget = new StubMiningTarget(noItemObject, Array.Empty<Guid>());

            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(twoItemTarget);
            var expected =
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(FirstEarnItemGuid))}, " +
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(SecondEarnItemGuid))}";
            Assert.AreEqual(expected, context.CurrentFocusTargetEarnItemNames);

            // 取得物ゼロの対象では名前欄を空に戻す
            // A target that yields nothing clears the name slot
            context.SetFocusTarget(noItemTarget);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(null);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            UnityEngine.Object.DestroyImmediate(twoItemObject);
            UnityEngine.Object.DestroyImmediate(noItemObject);
        }

        private class StubMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;

            // 取得回数を数えることで、SetFocusTargetの再解決有無をテストから観測できるようにする
            // Counts reads so tests can observe whether SetFocusTarget re-resolved this target
            public int EarnItemGuidsAccessCount { get; private set; }

            private readonly IReadOnlyList<Guid> _earnItemGuids;
            private readonly List<ItemId> _recommendedToolItemIds = new();

            public IReadOnlyList<Guid> EarnItemGuids
            {
                get
                {
                    EarnItemGuidsAccessCount++;
                    return _earnItemGuids;
                }
            }

            public StubMiningTarget(GameObject gameObject, IReadOnlyList<Guid> earnItemGuids)
            {
                GameObject = gameObject;
                _earnItemGuids = earnItemGuids;
            }

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
            {
                // フォーカス解決だけを見るfixtureなので採掘可否は問わない
                // This fixture only observes focus resolution, so minability is irrelevant
                tool = default;
                recommendedToolItemIds = _recommendedToolItemIds;
                return MiningStartOutcome.ToolMismatch;
            }

            public void SetHighlighted(bool highlighted)
            {
            }

            public void SendAttack()
            {
            }
        }
    }
}
