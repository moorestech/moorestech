using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest
{
    /// <summary>
    ///     ・待機操作の本体
    ///     ・待機表示
    ///     ・タイムライン記録
    ///     ・Wait operation implementations
    ///     ・Waiting overlay
    ///     ・Timeline recording
    /// </summary>
    internal class PlaytestWaitActions
    {
        private readonly PlaytestReporter _reporter;

        public PlaytestWaitActions(PlaytestReporter reporter)
        {
            _reporter = reporter;
        }

        public async UniTask UntilUiElement(string testid, float timeoutSeconds)
        {
            // 待機表示し成功後も操作間隔を確保
            // Show the wait and keep the action interval after success
            var waitEntry = _reporter.BeginWait($"UI要素待ち: {testid}");
            await PlaytestUiElementOps.WaitUiElement(testid, timeoutSeconds);
            _reporter.EndWait(waitEntry);
            await UniTask.Delay(TimeSpan.FromSeconds(PlaytestReporter.ActionIntervalSeconds), ignoreTimeScale: true);
        }

        public async UniTask WaitUiState(UIStateEnum state, float timeoutSeconds)
        {
            var waitEntry = _reporter.BeginWait($"UI状態待ち: {state}");
            await PlaytestUiOps.WaitUiState(state, timeoutSeconds);
            _reporter.EndWait(waitEntry);
        }

        public async UniTask<BlockGameObject> WaitBlockGameObject(Vector3Int position)
        {
            var waitEntry = _reporter.BeginWait($"ブロック生成待ち: {position}");
            var blockGameObject = await PlaytestBlockOps.WaitBlockGameObjectSpawn(position, 15f);
            _reporter.EndWait(waitEntry);
            return blockGameObject;
        }

        public async UniTask WaitSeconds(float seconds)
        {
            _reporter.BeginWait($"{seconds:0.#}秒");
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        }
    }
}
