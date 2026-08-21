using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     フォーカス状態が採掘可否の結果ごとに正しい遷移と提示を選ぶことを検証
    ///     Verify the focus state picks the right transition and presentation per mining outcome
    /// </summary>
    public class MiningFocusStateTest : InputTestFixture
    {
        private static readonly System.Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        private readonly List<GameObject> _stubTargetObjects = new();
        private GameObject _tooltipObject;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            InputSystem.AddDevice<Mouse>();
            ResetInputManagerCache();

            // 文言解決は実辞書を通す。キーが辞書から外れた退行もここで落ちる
            // Resolve text through the real dictionary so a key dropped from it fails here too
            Localize.Initialize();
            CreateTooltip();

            #region Internal

            void CreateTooltip()
            {
                _tooltipObject = new GameObject("MouseCursorTooltip");
                _tooltipObject.SetActive(false);
                var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
                SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
                SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
                _tooltipObject.SetActive(true);
                InvokePrivate(tooltip, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            foreach (var stubTargetObject in _stubTargetObjects)
                UnityEngine.Object.DestroyImmediate(stubTargetObject);
            _stubTargetObjects.Clear();
            UnityEngine.Object.DestroyImmediate(_tooltipObject);
            SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
            ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 採掘対象でなくなったらIdleへ戻る()
        {
            var next = RunFocusState(MiningStartOutcome.Unavailable);

            Assert.IsInstanceOf<MiningIdleState>(next);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 手掘り不可の対象には掘れない旨を提示してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.HandMiningNotAllowed, focusState);

            // 掘れないことを示す文言が本PRの目的なので、キーごと固定する
            // Declaring it unmineable is this PR's goal, so pin the very key
            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CannotHandMine.Key, MouseCursorTooltip.Instance.GetPresentation().Lines[0].TextKey);
        }

        [Test]
        public void 装備が合わない対象には必要ツールを提示してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.ToolMismatch, focusState);

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.RequiredItems.Key, MouseCursorTooltip.Instance.GetPresentation().Lines[0].TextKey);
        }

        [Test]
        public void 採掘可能でも未クリックなら押下を促してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.Ready, focusState);

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, MouseCursorTooltip.Instance.GetPresentation().Lines[0].TextKey);
        }

        private IMiningState RunFocusState(MiningStartOutcome outcome)
        {
            return RunFocusState(outcome, new MiningFocusState());
        }

        private IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState)
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            var stubTarget = new OutcomeStubMiningTarget(outcome, MasterHolder.ItemMaster.GetItemId(ToolItemGuid));
            _stubTargetObjects.Add(stubTarget.GameObject);
            context.SetFocusTarget(stubTarget);

            // 入力アセットを状態イベントより先に生成しないとバインドが解決されない
            // The input asset must be created before the state event, otherwise its bindings never resolve
            Assert.IsFalse(InputManager.Playable.ScreenLeftClick.GetKey, "左クリックが押されていない前提が崩れている");
            return focusState.GetNextUpdate(context, 0.01f);
        }

        private static LocalPlayerEquipment CreateEquipmentHoldingTool()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }

        // InputTestFixtureがInputSystemを差し替えるため、他セッションで作られた入力アセットは捨てて張り直す
        // InputTestFixture swaps the InputSystem, so an input asset built in another session must be dropped and rebuilt
        private static void ResetInputManagerCache()
        {
            foreach (var fieldName in new[] { "_instance", "player", "playable", "ui" })
            {
                typeof(InputManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type targetType, string propertyName, object value)
        {
            targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private sealed class OutcomeStubMiningTarget : IMiningTargetObject
        {
            private readonly MiningStartOutcome _outcome;
            private readonly List<ItemId> _recommendedToolItemIds;

            public GameObject GameObject { get; }
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;

            public OutcomeStubMiningTarget(MiningStartOutcome outcome, ItemId recommendedToolItemId)
            {
                _outcome = outcome;
                _recommendedToolItemIds = new List<ItemId> { recommendedToolItemId };
                GameObject = new GameObject("OutcomeStubMiningTarget");
            }

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
            {
                tool = new MiningToolCandidate(equippedItemId, 1f);
                recommendedToolItemIds = _recommendedToolItemIds;
                return _outcome;
            }

            public void SetFocused(bool focused)
            {
            }

            public void SendAttack()
            {
            }
        }
    }
}
