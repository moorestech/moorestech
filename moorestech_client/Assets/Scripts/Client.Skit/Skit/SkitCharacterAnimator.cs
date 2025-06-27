using Client.Skit.Define;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Client.Skit.Skit
{
    /// <summary>AnimationClip のシンプルなクロスフェード再生器</summary>
    public class SkitCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private AnimationDefine _animationDefine;

        // Playables
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _currentPlayable;
        private AnimationClipPlayable _nextPlayable;

        // フェード制御
        private bool  _isFading;
        private float _fadeDuration;
        private float _fadeElapsed;

        // 初期化
        public void Initialize(AnimationDefine animationDefine)
        {
            _animationDefine = animationDefine;
            EnsureGraph();                        // 呼ばれた時点でグラフを生成
        }

        /// <summary>animationId を fadeDuration 秒でクロスフェード</summary>
        public void PlayAnimation(string animationId, float fadeDuration = 0.25f)
        {
            if (_animationDefine == null)
            {
                Debug.LogWarning($"{nameof(SkitCharacterAnimator)} : Initialize が呼ばれていません");
                return;
            }

            var clip = _animationDefine.GetAnimationClip(animationId);
            if (clip == null)
            {
                Debug.LogWarning($"{nameof(SkitCharacterAnimator)} : AnimationClip '{animationId}' が見つかりません");
                return;
            }

            EnsureGraph();

            // 既に同じクリップを再生中で、フェードもしていなければ無視
            if (_currentPlayable.IsValid() &&
                _currentPlayable.GetAnimationClip() == clip &&
                !_isFading)
            {
                return;
            }

            // 以前準備していた nextPlayable をクリア
            if (_nextPlayable.IsValid())
            {
                _graph.Disconnect(_mixer, 1);
                _nextPlayable.Destroy();
            }

            // 次クリップを 1 番スロットに接続
            _nextPlayable = AnimationClipPlayable.Create(_graph, clip);
            _nextPlayable.SetApplyFootIK(false);
            _graph.Connect(_nextPlayable, 0, _mixer, 1);
            _mixer.SetInputWeight(1, 0f);

            // フェード開始
            _fadeDuration = Mathf.Max(0.0001f, fadeDuration);
            _fadeElapsed  = 0f;
            _isFading     = true;
        }

        private void Update()
        {
            if (!_isFading) return;

            _fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);

            // ウェイト補間
            _mixer.SetInputWeight(0, 1f - t);
            _mixer.SetInputWeight(1, t);

            // フェード終了判定
            if (t >= 1f)
            {
                // 古いクリップを破棄
                if (_currentPlayable.IsValid())
                {
                    _graph.Disconnect(_mixer, 0);
                    _currentPlayable.Destroy();
                }

                // next を current として 0 番へ
                _currentPlayable = _nextPlayable;
                _nextPlayable    = default;

                _graph.Disconnect(_mixer, 1);
                _graph.Connect(_currentPlayable, 0, _mixer, 0);
                _mixer.SetInputWeight(0, 1f);

                _isFading = false;
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        /// <summary>PlayableGraph を生成（既にあれば何もしない）</summary>
        private void EnsureGraph()
        {
            if (_graph.IsValid()) return;

            _graph = PlayableGraph.Create($"{nameof(SkitCharacterAnimator)}_{name}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _mixer = AnimationMixerPlayable.Create(_graph, 2, true);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            output.SetSourcePlayable(_mixer);

            _graph.Play();
        }
    }
}
