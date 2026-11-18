using System;

namespace Client.Skit.UI
{
    /// <summary>
    ///     スキットの許可intentの宣言・ポーズ中の空化・受け付け判定を持つ
    ///     Owns the declared skit intents, their collapse while paused, and the intent validation
    /// </summary>
    public class SkitIntentGate
    {
        // シーンが宣言した許可intent。入力停止中でも復帰時にそのまま戻せるよう保持する
        // The intents the scene declared, kept so they come back untouched when input resumes
        private string[] _authoredIntents = Array.Empty<string>();
        private bool _inputSuspended;

        // 本文の送り待ちで許可するintentを宣言する
        // Declare the intents allowed while the text waits for advance
        public string[] DeclareTextIntents()
        {
            return Declare(new[]
            {
                SkitPresentationData.AdvanceIntent, SkitPresentationData.SetAutoIntent,
                SkitPresentationData.SkipIntent, SkitPresentationData.SetUiHiddenIntent,
            });
        }

        // 選択待ちで許可するintentを宣言する
        // Declare the intents allowed while choices wait for a selection
        public string[] DeclareChoiceIntents()
        {
            return Declare(new[]
            {
                SkitPresentationData.SelectIntent, SkitPresentationData.SetAutoIntent,
                SkitPresentationData.SkipIntent, SkitPresentationData.SetUiHiddenIntent,
            });
        }

        public string[] DeclareNoIntents()
        {
            return Declare(Array.Empty<string>());
        }

        // 入力停止を切り替え、変わったかを返す
        // Switch input suspension and report whether it changed
        public bool TrySetInputSuspended(bool suspended)
        {
            if (_inputSuspended == suspended) return false;
            _inputSuspended = suspended;
            return true;
        }

        // ポーズ中は許可intentを空へ畳んだ形を配信する
        // While paused, publish the allowed intents collapsed to empty
        public string[] PublishedIntents()
        {
            return _inputSuspended ? Array.Empty<string>() : _authoredIntents;
        }

        // session・revision・許可の順に照合し、最初に外れた理由を返す
        // Check session, revision, then the allowlist, returning the first failing reason
        public SkitIntentResult Validate(SkitPresentationData current, string sessionId, int revision, string intent)
        {
            if (sessionId != current.SessionId) return SkitIntentResult.Fail("stale_session");
            if (revision != current.SceneRevision) return SkitIntentResult.Fail("stale_revision");
            if (!SkitPresentationData.IsIntentAllowed(current.AllowedIntents, intent))
                return SkitIntentResult.Fail("intent_not_allowed");
            return SkitIntentResult.Success();
        }

        private string[] Declare(string[] intents)
        {
            _authoredIntents = intents;
            return PublishedIntents();
        }
    }
}
