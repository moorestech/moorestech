using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     フォーカス状態テストが共有する土台（実辞書・実tooltip・結果別スタブ）
    ///     Shared ground for the focus state tests: real dictionary, real tooltip and outcome stubs
    /// </summary>
    public abstract class MiningFocusStateTestFixture : InputTestFixture
    {
        protected static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid EarnItemGuid = new("00000000-0000-0000-9999-000000000001");

        private readonly List<GameObject> _stubTargetObjects = new();
        private GameObject _tooltipObject;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();

            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary so a key dropped from it fails here too
            Localize.Initialize();
            CreateTooltip();

            #region Internal

            void CreateTooltip()
            {
                _tooltipObject = new GameObject("MouseCursorTooltip");
                _tooltipObject.SetActive(false);
                var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
                TestReflection.SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
                TestReflection.SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
                _tooltipObject.SetActive(true);
                TestReflection.InvokePrivate(tooltip, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            foreach (var stubTargetObject in _stubTargetObjects)
                UnityEngine.Object.DestroyImmediate(stubTargetObject);
            _stubTargetObjects.Clear();
            UnityEngine.Object.DestroyImmediate(_tooltipObject);
            TestReflection.SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        protected IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState)
        {
            return RunFocusState(outcome, focusState, Array.Empty<Guid>());
        }

        protected IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState, IReadOnlyList<Guid> earnItemGuids)
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            var stubTarget = new OutcomeStubMiningTarget(outcome, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), earnItemGuids);
            _stubTargetObjects.Add(stubTarget.GameObject);
            context.SetFocusTarget(stubTarget);

            // 入力アセットは状態イベントより先に作る
            // The input asset must be created before the state event, otherwise its bindings never resolve
            Assert.IsFalse(InputManager.Playable.Interact.GetKey, "Fが押されていない前提が崩れている");
            return focusState.GetNextUpdate(context, 0.01f);
        }

        // 表示中の1行目のキー文字列（提示は1行構成が前提）
        // Key string of the first shown line; the presentation is expected to be one line
        protected static string ShownTooltipKey()
        {
            return MouseCursorTooltip.Instance.GetPresentation().Lines[0].Key.Key;
        }

        protected static IReadOnlyList<string> ShownTooltipParams()
        {
            return MouseCursorTooltip.Instance.GetPresentation().Lines[0].TextParams;
        }

        private static LocalPlayerEquipment CreateEquipmentHoldingTool()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }

        private sealed class OutcomeStubMiningTarget : IMiningTargetObject
        {
            private readonly MiningStartOutcome _outcome;
            private readonly List<ItemId> _recommendedToolItemIds;

            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public IReadOnlyList<Guid> EarnItemGuids { get; }

            public OutcomeStubMiningTarget(MiningStartOutcome outcome, ItemId recommendedToolItemId, IReadOnlyList<Guid> earnItemGuids)
            {
                _outcome = outcome;
                _recommendedToolItemIds = new List<ItemId> { recommendedToolItemId };
                EarnItemGuids = earnItemGuids;
                GameObject = new GameObject("OutcomeStubMiningTarget");
            }

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
            {
                tool = new MiningToolCandidate(equippedItemId, 1f);
                recommendedToolItemIds = _recommendedToolItemIds;
                return _outcome;
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
