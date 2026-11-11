using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Ui.State.Skit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Skit
{
    /// <summary>
    /// 実物の store の提示から、出す面と押せる操作が Web の SkitPresentation の分岐どおりに決まることを確かめる（L0）
    /// Verifies that the surface and the pressable operations follow the Web SkitPresentation branches for real store snapshots (L0)
    /// </summary>
    public class SkitViewModelTest
    {
        private SkitPresentationStateStore _store;

        [SetUp]
        public void CreateStore()
        {
            _store = new SkitPresentationStateStore();
        }

        [Test]
        public void 本文の提示では窓を出し送り自動スキップUI非表示を押せる()
        {
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", "Blocking message");
            var model = SkitViewModel.From(_store.GetCurrent());

            Assert.AreEqual(SkitSurface.Window, model.Surface);
            Assert.AreEqual("Moore", model.SpeakerName);
            Assert.AreEqual("Blocking message", model.Body);
            Assert.IsTrue(model.CanAdvance && model.CanSetAuto && model.CanSkip && model.CanSetUiHidden);
            Assert.AreEqual(0, model.Choices.Count);
            Assert.IsTrue(model.RevealsByTypewriter);
            Assert.AreEqual(50, model.RevealIntervalMs);
        }

        [Test]
        public void 選択肢の提示では選択肢を出し送りは押せない()
        {
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", "Blocking message");
            _store.PresentChoices(new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" }, new SkitChoice { ChoiceId = "route-b", Label = "Route B" } });
            var model = SkitViewModel.From(_store.GetCurrent());

            Assert.AreEqual(SkitSurface.Window, model.Surface);
            CollectionAssert.AreEqual(new[] { "route-a", "route-b" }, model.Choices.Select(choice => choice.ChoiceId).ToArray());
            CollectionAssert.AreEqual(new[] { "Route A", "Route B" }, model.Choices.Select(choice => choice.Label).ToArray());
            Assert.IsFalse(model.CanAdvance);
        }

        [Test]
        public void ポーズで許可が空になると全操作が閉じ選択肢も出さない()
        {
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentChoices(new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" } });
            _store.SetInputSuspended(true);
            var model = SkitViewModel.From(_store.GetCurrent());

            Assert.AreEqual(SkitSurface.Window, model.Surface);
            Assert.IsFalse(model.CanAdvance || model.CanSetAuto || model.CanSkip || model.CanSetUiHidden);
            Assert.AreEqual(0, model.Choices.Count);
        }

        [Test]
        public void UI非表示の間は復帰の面だけを出す()
        {
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", "Blocking message");
            var current = _store.GetCurrent();
            Assert.IsTrue(_store.TrySetUiHidden(current.SessionId, current.SceneRevision, true).Ok);

            Assert.AreEqual(SkitSurface.RestoreOnly, SkitViewModel.From(_store.GetCurrent()).Surface);
        }

        [Test]
        public void 演出で本文の窓が閉じている間はblockingはツールだけで背景は何も出さない()
        {
            _store.BeginBlocking(new SkitActionContext());
            _store.SetTextAreaVisible(false);
            Assert.AreEqual(SkitSurface.ToolbarOnly, SkitViewModel.From(_store.GetCurrent()).Surface);

            _store.BeginBackground();
            _store.SetBackgroundText("Moore", "Background message");
            _store.SetTextAreaVisible(false);
            Assert.AreEqual(SkitSurface.None, SkitViewModel.From(_store.GetCurrent()).Surface);
        }

        [Test]
        public void 背景は本文が空なら出さず本文が来たら1行を出し終わると何も出さない()
        {
            _store.BeginBackground();
            var empty = SkitViewModel.From(_store.GetCurrent());
            Assert.AreEqual(SkitSurface.None, empty.Surface);
            Assert.IsTrue(empty.IsPresenting);

            _store.SetBackgroundText("Moore", "Background message");
            var line = SkitViewModel.From(_store.GetCurrent());
            Assert.AreEqual(SkitSurface.BackgroundLine, line.Surface);
            Assert.AreEqual(24, line.RevealIntervalMs);

            _store.End();
            var ended = SkitViewModel.From(_store.GetCurrent());
            Assert.AreEqual(SkitSurface.None, ended.Surface);
            Assert.IsFalse(ended.IsPresenting);
            Assert.IsFalse(ended.TransitionVisible);
        }

        [Test]
        public void 未知のモードは開発者ログを出して何も出さない()
        {
            var data = new SkitPresentationData
            {
                SessionId = "s",
                SceneRevision = 0,
                AllowedIntents = Array.Empty<string>(),
                PresentationState = new SkitPresentationState { Mode = "cinematic", SpeakerName = "", Body = "", Choices = Array.Empty<SkitChoice>(), TextAreaVisible = true, TextReveal = new SkitTextReveal { Mode = SkitPresentationData.InstantRevealMode, IntervalMs = 0 } },
            };
            LogAssert.Expect(LogType.Error, new Regex("cinematic"));

            Assert.AreEqual(SkitSurface.None, SkitViewModel.From(data).Surface);
        }
    }
}
