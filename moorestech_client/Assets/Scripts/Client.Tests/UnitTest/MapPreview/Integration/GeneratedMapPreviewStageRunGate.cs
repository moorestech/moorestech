using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.MapScene.Editor;
using Cysharp.Threading.Tasks;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal sealed class GeneratedMapPreviewStageRunGate
    {
        private readonly GeneratedMapPreviewStage _stage;
        private readonly GeneratedMapPreviewRun _run;
        private readonly CancellationToken _cancellationToken;
        internal readonly UniTaskCompletionSource WorldBuilt = new();
        internal readonly UniTaskCompletionSource Release = new();
        internal readonly Task Completion;

        internal GeneratedMapPreviewStageRunGate(GeneratedMapPreviewStage stage, GeneratedMapPreviewRun run,
            CancellationTokenSource cancellation)
        {
            _stage = stage;
            _run = run;
            _cancellationToken = cancellation.Token;

            // 実Runを私有監視へ渡す。
            // Feed the real run into the existing private observer without expanding the production API.
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GeneratedMapPreviewStage).GetField("_run", privateInstance).SetValue(stage, run);
            typeof(GeneratedMapPreviewStage).GetField("_cancellation", privateInstance).SetValue(stage, cancellation);
            typeof(GeneratedMapPreviewStage).GetProperty(nameof(stage.State)).SetValue(stage, GeneratedMapPreviewState.Generating);
            var observer = typeof(GeneratedMapPreviewStage).GetMethod("GenerateAsync", privateInstance);
            Completion = ((UniTask)observer.Invoke(stage,
                new object[] { run, ExecuteRealRunThenHold(), _cancellationToken })).AsTask();
        }

        private async UniTask ExecuteRealRunThenHold()
        {
            await _run.ExecuteAsync(_stage.scene, _cancellationToken);
            WorldBuilt.TrySetResult();
            await Release.Task;
        }
    }
}
