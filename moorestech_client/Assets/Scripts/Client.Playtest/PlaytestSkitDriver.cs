using Client.Playtest.Core;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;

namespace Client.Playtest
{
    /// <summary>
    ///     開幕スキットを飛ばす口の部品。PlaytestDriver が委ねる
    ///     Opening-skit skip ports that PlaytestDriver delegates to
    /// </summary>
    internal class PlaytestSkitDriver
    {
        // 開幕スキットのSkip受理待ちと、その後のGameScreen到達待ちの上限
        // Timeouts for the opening skit's skip acceptance and the subsequent arrival at GameScreen
        private const float SkitSkipTimeoutSeconds = 30f;
        private const float SkitSkipUiStateTimeoutSeconds = 15f;
        // 重い機械では 1 回の Skip で終わらないので、Story のままなら飛ばし直す回数と 1 回ごとの待ち
        // On a loaded machine one skip may not end it, so the retry count and per-round wait while still in Story
        private const int SkipUntilLeftStoryMaxRounds = 4;
        private const float SkipUntilLeftStoryPollSeconds = 5f;

        private readonly PlaytestReporter _reporter;

        public PlaytestSkitDriver(PlaytestReporter reporter)
        {
            _reporter = reporter;
        }

        public async UniTask SkipOpeningSkit() => await _reporter.Act("開幕スキットをSkipインテントで飛ばす", () => PlaytestSkitOps.SkipOpeningSkit(SkitSkipTimeoutSeconds, SkitSkipUiStateTimeoutSeconds));

        public async UniTask SkipOpeningSkitIfPlaying() => await _reporter.Act("開幕スキットを再生中なら飛ばす", () => PlaytestSkitOps.SkipOpeningSkitIfPlaying(PlaytestSkitOps.OptionalSkipTimeoutSeconds));

        public async UniTask<int> SkipOpeningSkitUntilLeftStory()
        {
            // 回数は Act の外へ持ち出し、Note は口のこの 1 か所で出す
            // Carry the round count out of Act and emit the Note only here
            var rounds = 0;
            await _reporter.Act("開幕スキットを Story を抜けるまで飛ばす", async () => rounds = await PlaytestSkitOps.SkipOpeningSkitUntilLeftStory(SkipUntilLeftStoryMaxRounds, SkipUntilLeftStoryPollSeconds));
            _reporter.Note($"opening-skit-skip-rounds={rounds}");
            return rounds;
        }
    }
}
