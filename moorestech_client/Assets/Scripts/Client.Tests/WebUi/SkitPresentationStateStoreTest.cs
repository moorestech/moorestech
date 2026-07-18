using System;
using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Skit.Context;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.WebUi
{
    public class SkitPresentationStateStoreTest
    {
        private SkitPresentationStateStore _store;
        private StubSkitActionController _controller;

        [SetUp]
        public void SetUp()
        {
            _store = new SkitPresentationStateStore();
            _controller = new StubSkitActionController();
            _store.BeginBlocking(_controller);
        }

        // 本文待機は advance を一度だけ受理し、即座に revision を進める
        // A text wait accepts advance once and immediately advances its revision
        [Test]
        public void AdvanceReleasesCurrentTextWaitOnlyOnce()
        {
            _store.PresentBlockingText("Moore", "Hello");
            var sessionId = _store.GetCurrent().SessionId;
            var revision = _store.GetCurrent().SceneRevision;

            var first = _store.TryAdvance(sessionId, revision);
            var second = _store.TryAdvance(sessionId, revision);

            Assert.IsTrue(first.Ok);
            Assert.AreEqual("stale_revision", second.Error);
            Assert.AreEqual(revision + 1, _store.GetCurrent().SceneRevision);
        }

        // 古い session は現在の待機状態を変更しない
        // A stale session does not mutate the current wait state
        [Test]
        public void StaleSessionHasNoSideEffect()
        {
            _store.PresentBlockingText("Moore", "Hello");
            var before = _store.GetCurrent();

            var result = _store.TryAdvance("old-session", before.SceneRevision);

            Assert.AreEqual("stale_session", result.Error);
            Assert.AreSame(before, _store.GetCurrent());
        }

        // 現在許可されていない select は副作用なく拒否する
        // A select intent that is not currently allowed is rejected without side effects
        [Test]
        public void DisallowedSelectHasNoSideEffect()
        {
            _store.PresentBlockingText("Moore", "Hello");
            var before = _store.GetCurrent();

            var result = _store.TrySelect(before.SessionId, before.SceneRevision, "choice-a");

            Assert.AreEqual("intent_not_allowed", result.Error);
            Assert.AreSame(before, _store.GetCurrent());
        }

        // 未知 choiceId は選択待機を解放しない
        // An unknown choiceId does not release the selection wait
        [Test]
        public void UnknownChoiceHasNoSideEffect()
        {
            _store.PresentChoices(new[] { new SkitChoice { ChoiceId = "choice-a", Label = "A" } });
            var before = _store.GetCurrent();

            var result = _store.TrySelect(before.SessionId, before.SceneRevision, "choice-x");

            Assert.AreEqual("unknown_choice", result.Error);
            Assert.AreSame(before, _store.GetCurrent());
        }

        // choiceId から表示順を解決し、同じ位置の jump target を選べる
        // Resolve a choiceId to its display slot so Unity can select the matching jump target
        [Test]
        public void ChoiceIdResolvesMatchingJumpSlot()
        {
            var choices = new[]
            {
                new SkitChoice { ChoiceId = "choice-a", Label = "A" },
                new SkitChoice { ChoiceId = "choice-b", Label = "B" },
            };

            Assert.AreEqual(1, SkitChoiceJumpResolver.ResolveSelectedIndex("choice-b", choices));
        }

        // command結果のjump targetへ移動し、中間commandを実行しない
        // Move to the command result's jump target without executing commands in between
        [Test]
        public async System.Threading.Tasks.Task CommandExecutorAppliesJumpResult()
        {
            var first = new StubCommand(1, (CommandId)3);
            var skipped = new StubCommand(2, null);
            var target = new StubCommand(3, null);

            await SkitCommandExecutor.ExecuteAsync(new ICommandForgeCommand[] { first, skipped, target }, null);

            Assert.IsTrue(first.Executed);
            Assert.IsFalse(skipped.Executed);
            Assert.IsTrue(target.Executed);
        }

        private class StubSkitActionController : ISkitActionController
        {
            private readonly Subject<Unit> _onSkip = new();
            public bool IsAuto { get; private set; }
            public bool IsSkip { get; private set; }
            public IObservable<Unit> OnSkip => _onSkip;

            public void SetAuto(bool isAuto)
            {
                IsAuto = isAuto;
            }

            public void SetSkip(bool isSkip)
            {
                IsSkip = isSkip;
                if (isSkip) _onSkip.OnNext(Unit.Default);
            }
        }

        private class StubCommand : ICommandForgeCommand
        {
            public readonly CommandId CommandId;
            private readonly CommandId? _jumpTarget;
            public bool Executed;

            public StubCommand(int commandId, CommandId? jumpTarget)
            {
                CommandId = (CommandId)commandId;
                _jumpTarget = jumpTarget;
            }

            public UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
            {
                Executed = true;
                var result = _jumpTarget.HasValue
                    ? new CommandResultContext { JumpTargetCommandId = _jumpTarget.Value }
                    : null;
                return UniTask.FromResult(result);
            }
        }
    }
}
