using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Client.Skit.Skit
{
    /// <summary>
    /// Addressables で取得した AnimationClip を
    /// Animator（PlayableGraph）に直接流し込んでクロスフェード再生するユーティリティ。
    /// ・AnimatorController は不要
    /// ・fadeDuration 秒でクロスフェード
    /// ・フェード完了後はループ再生し続ける（API 非依存でループ保証）
    /// </summary>
    public sealed class SkitCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        PlayableGraph            _graph;
        AnimationMixerPlayable   _mixer;          // 2 入力ミキサー
        AnimationClipPlayable    _current;        // ループ中
        AnimationClipPlayable    _next;           // フェード先
        bool   _isFading;
        float  _fadeDuration;
        float  _fadeElapsed;

        public void Initialize()
        {
            animator.runtimeAnimatorController = null;   // Controller 不要

            _graph = PlayableGraph.Create($"{name}_AnimGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _mixer = AnimationMixerPlayable.Create(_graph, 2, true);
            var output = AnimationPlayableOutput.Create(_graph, "Anim", animator);
            output.SetSourcePlayable(_mixer);

            _graph.Play();
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        /// <summary>
        /// Addressable の animationId を fadeDuration 秒かけてクロスフェード。
        /// その後は永続ループ。
        /// </summary>
        public async UniTask PlayAnimation(string animationId, float fadeDuration = 0.25f)
        {
            var clip = await AddressableLoader.LoadAsyncDefault<AnimationClip>(animationId);
            if (clip == null)
            {
                Debug.LogError($"{nameof(SkitCharacterAnimator)} : AnimationClip '{animationId}' が見つかりません");
                return;
            }

            // Playable 作成
            PrepareNextPlayable(clip);

            _fadeDuration = Mathf.Max(0.0001f, fadeDuration);
            _fadeElapsed  = 0f;
            _isFading     = _current.IsValid();        // 既に再生中ならフェード

            _mixer.SetInputWeight(0, _isFading ? 1f : 0f); // current
            _mixer.SetInputWeight(1, _isFading ? 0f : 1f); // next
        }

        void Update()
        {
            // フェード進行
            if (_isFading)
            {
                _fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);

                _mixer.SetInputWeight(0, 1f - t);
                _mixer.SetInputWeight(1, t);

                if (t >= 1f) EndFade();
            }

            // 手動ループ：末尾に達したら時間を 0 に戻す
            if (_current.IsValid())
            {
                double len  = _current.GetAnimationClip().length;
                if (len > 0 && _current.GetTime() >= len)
                    _current.SetTime(0);
            }
        }

        // ─ 内部ユーティリティ ───────────────────────────────
        void PrepareNextPlayable(AnimationClip clip)
        {
            if (_next.IsValid())
            {
                _mixer.DisconnectInput(1);
                _next.Destroy();
            }

            _next = AnimationClipPlayable.Create(_graph, clip);
            _next.SetApplyFootIK(false);

            _mixer.ConnectInput(1, _next, 0);
            _mixer.SetInputWeight(1, 0f);
            _next.Play();
        }

        void EndFade()
        {
            if (_current.IsValid())
            {
                _mixer.DisconnectInput(0);
                _current.Destroy();
            }

            _current = _next;
            _next    = default;

            _mixer.DisconnectInput(0);
            _mixer.ConnectInput(0, _current, 0);
            _mixer.SetInputWeight(0, 1f);

            _isFading = false;
        }
    }
}
