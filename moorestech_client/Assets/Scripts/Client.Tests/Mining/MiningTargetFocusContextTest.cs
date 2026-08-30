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
            var firstTarget = new FocusTrackingMiningTarget("first", sharedGameObject, new List<string>(), Array.Empty<Guid>());
            var secondTarget = new FocusTrackingMiningTarget("second", new GameObject("Second"), new List<string>(), Array.Empty<Guid>());
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(firstTarget);
            Assert.AreSame(firstTarget, context.CurrentFocusTarget);
            context.SetFocusTarget(secondTarget);
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
            var focusEventLog = new List<string>();
            var twoItemObject = new GameObject("TwoItemTarget");
            var noItemObject = new GameObject("NoItemTarget");
            var twoItemTarget = new FocusTrackingMiningTarget("two", twoItemObject, focusEventLog, new[] { FirstEarnItemGuid, SecondEarnItemGuid });
            var noItemTarget = new FocusTrackingMiningTarget("none", noItemObject, focusEventLog, Array.Empty<Guid>());

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

        private class FocusTrackingMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public IReadOnlyList<Guid> EarnItemGuids { get; }
            public int FocusEnabledCount { get; private set; }
            public int FocusDisabledCount { get; private set; }
            private readonly List<ItemId> _recommendedToolItemIds = new();
            private readonly string _name;
            private readonly List<string> _focusEventLog;

            public FocusTrackingMiningTarget(string name, GameObject gameObject, List<string> focusEventLog, IReadOnlyList<Guid> earnItemGuids)
            {
                _name = name;
                GameObject = gameObject;
                _focusEventLog = focusEventLog;
                EarnItemGuids = earnItemGuids;
            }

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
            {
                // フォーカス通知だけを見るfixtureなので採掘可否は問わない
                // This fixture only observes focus notifications, so minability is irrelevant
                tool = default;
                recommendedToolItemIds = _recommendedToolItemIds;
                return MiningStartOutcome.ToolMismatch;
            }

            public void SetHighlighted(bool highlighted)
            {
                _focusEventLog.Add($"{_name}:{highlighted.ToString().ToLowerInvariant()}");
                if (highlighted)
                    FocusEnabledCount++;
                else
                    FocusDisabledCount++;
            }

            public void SendAttack()
            {
            }
        }
    }
}
