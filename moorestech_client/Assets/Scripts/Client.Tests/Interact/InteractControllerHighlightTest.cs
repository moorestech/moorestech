using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Tests.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     ハイライトは対象変化時のみ切替（旧実装の移設先）
    ///     Verifies the highlight toggles only when the target changes; moved here from the old MiningControllerContext
    /// </summary>
    public class InteractControllerHighlightTest
    {
        private readonly List<GameObject> _createdObjects = new();
        private GameObject _tooltipObject;

        [SetUp]
        public void SetUp()
        {
            // MiningIdleStateの生成がtooltipを触るため実体を用意する
            // Creating a MiningIdleState touches the tooltip, so a real one is required
            _tooltipObject = new GameObject("MouseCursorTooltip");
            _tooltipObject.SetActive(false);
            var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
            TestReflection.SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
            TestReflection.SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
            _tooltipObject.SetActive(true);
            TestReflection.InvokePrivate(tooltip, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects) Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
            Object.DestroyImmediate(_tooltipObject);
            TestReflection.SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
        }

        [Test]
        public void ハイライトは対象が変わった時だけ切り替わり消失時は一度だけ消える()
        {
            var log = new List<string>();
            var first = CreateTrackingInteractable("first", log);
            var second = CreateTrackingInteractable("second", log);
            var selector = new ScriptedInteractTargetSelector();
            var controller = new InteractController(null, selector, new ProgressBarState());

            // 同じ対象を掴み続けても点灯は一度きり
            // Holding the same target lights it up exactly once
            selector.SetNext(first);
            controller.ManualUpdate();
            controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true" }, log);

            selector.SetNext(second);
            controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true", "first:false", "second:true" }, log);

            // 対象を失った時の消灯も一度きり
            // Losing the target extinguishes it exactly once
            selector.SetNext(null);
            controller.ManualUpdate();
            controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true", "first:false", "second:true", "second:false" }, log);

            // 他UIステート離脱時は対象を消す
            // Leaving for another UI state extinguishes whatever was held
            selector.SetNext(first);
            controller.ManualUpdate();
            controller.Disable();
            CollectionAssert.AreEqual(
                new[] { "first:true", "first:false", "second:true", "second:false", "first:true", "first:false" }, log);
        }

        private HighlightTrackingInteractable CreateTrackingInteractable(string name, List<string> log)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return new HighlightTrackingInteractable(gameObject, name, log);
        }

        // 単押しも採掘も持たない対象。ハイライト以外の経路を走らせない
        // A target with neither tap nor mining behaviour, so no path but the highlight runs
        private sealed class HighlightTrackingInteractable : IInteractable
        {
            private readonly List<string> _log;
            private readonly string _name;

            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;

            public HighlightTrackingInteractable(GameObject gameObject, string name, List<string> log)
            {
                GameObject = gameObject;
                _name = name;
                _log = log;
            }

            public void SetHighlighted(bool highlighted)
            {
                _log.Add($"{_name}:{highlighted.ToString().ToLowerInvariant()}");
            }
        }
    }
}
