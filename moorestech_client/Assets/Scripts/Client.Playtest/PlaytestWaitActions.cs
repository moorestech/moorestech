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
    ///     PlaytestDriverの待機系操作の本体。待機中の表示とTimeline記録を伴う
    ///     Bodies of PlaytestDriver's wait operations, with the waiting overlay and Timeline record
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
            // 要素待ちを待機表示し、成功後も録画用の操作間隔を確保する
            // Show element waiting and retain the recording pace after success
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
