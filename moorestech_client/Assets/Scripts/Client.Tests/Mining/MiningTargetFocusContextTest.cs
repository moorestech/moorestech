using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining
{
    public class MiningTargetFocusContextTest
    {
        [Test]
        public void SetFocusTargetPushesOnlyWhenTargetChanges()
        {
            var context = new MapObjectMiningControllerContext(null);
            var focusEventLog = new List<string>();
            var sharedGameObject = new GameObject("SharedTarget");
            var secondGameObject = new GameObject("SecondTarget");
            var firstTarget = new FocusTrackingMiningTarget("first", sharedGameObject, focusEventLog);
            var sameObjectWrapper = new FocusTrackingMiningTarget("same-object-wrapper", sharedGameObject, focusEventLog);
            var secondTarget = new FocusTrackingMiningTarget("second", secondGameObject, focusEventLog);

            // 同じ実体の別wrapperへ更新しても通知を増やさず、最新wrapperは保持する
            // Updating to another wrapper of the same object keeps the latest wrapper without extra notifications
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(sameObjectWrapper);
            Assert.AreEqual(1, focusEventLog.Count);
            Assert.AreEqual(1, firstTarget.FocusEnabledCount);
            Assert.AreEqual(0, firstTarget.FocusDisabledCount);
            Assert.AreEqual(0, sameObjectWrapper.FocusEnabledCount);
            Assert.AreSame(sameObjectWrapper, context.CurrentFocusTarget);

            // 実体変更時は最新wrapperの解除を新対象の有効化より先に通知する
            // A concrete-object change defocuses the latest wrapper before focusing the new target
            focusEventLog.Clear();
            context.SetFocusTarget(secondTarget);
            CollectionAssert.AreEqual(
                new[] { "same-object-wrapper:false", "second:true" },
                focusEventLog);
            Assert.AreEqual(1, sameObjectWrapper.FocusDisabledCount);
            Assert.AreEqual(1, secondTarget.FocusEnabledCount);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);

            // 対象が消えた場合も最後の対象へ解除を一度だけ通知する
            // Losing the target also sends exactly one defocus notification to the last target
            focusEventLog.Clear();
            context.SetFocusTarget(null);
            context.SetFocusTarget(null);
            CollectionAssert.AreEqual(new[] { "second:false" }, focusEventLog);
            Assert.AreEqual(1, secondTarget.FocusDisabledCount);
            Assert.IsNull(context.CurrentFocusTarget);

            Object.DestroyImmediate(sharedGameObject);
            Object.DestroyImmediate(secondGameObject);
        }

        private class FocusTrackingMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public bool IsAvailable => true;
            public bool IsPickUp => false;
            public List<ItemId> UsableToolItemIds { get; } = new();
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public int FocusEnabledCount { get; private set; }
            public int FocusDisabledCount { get; private set; }
            private readonly string _name;
            private readonly List<string> _focusEventLog;

            public FocusTrackingMiningTarget(string name, GameObject gameObject, List<string> focusEventLog)
            {
                _name = name;
                GameObject = gameObject;
                _focusEventLog = focusEventLog;
            }

            public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
            {
                tool = default;
                return false;
            }

            public void SetFocused(bool focused)
            {
                _focusEventLog.Add($"{_name}:{focused.ToString().ToLowerInvariant()}");
                if (focused)
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
