using Client.Skit.UI;
using UniRx;

namespace Client.Ui.State.SkitPresence
{
    /// <summary>
    /// blocking スキット（会話の演出が画面を持つ間）かを配る。常駐 HUD はこれが真の間だけ退く（Web の useBlockingSkitActive）
    /// Publishes whether a blocking skit owns the screen; resident HUDs withdraw only while it is true (Web useBlockingSkitActive)
    /// </summary>
    /// <remarks>
    /// 背景スキット（mode "background"）は数えない。使う画面は S2-hud のキーヒント・進行中チャレンジ、S2-tutorial のキー操作ヒント・ワールドピン、S3-hotbar-modal のホットバー（設計書 A-56）
    /// Background skits (mode "background") do not count; users are S2-hud key hints and current challenge, S2-tutorial key control hints and world pins, and the S3-hotbar-modal hotbar (design A-56)
    /// </remarks>
    public sealed class BlockingSkitPresence
    {
        private readonly ReactiveProperty<bool> _isBlocking;

        public IReadOnlyReactiveProperty<bool> IsBlocking => _isBlocking;

        public BlockingSkitPresence(SkitPresentationStateStore store)
        {
            // 今の値で始め、以後は store の変化だけで更新する。ゲーム寿命なので購読は解かない
            // Start from the current value and follow store changes; it lives for the whole game, so the subscription is never released
            _isBlocking = new ReactiveProperty<bool>(IsBlockingMode(store.GetCurrent()));
            store.ObserveChanged().Subscribe(data => _isBlocking.Value = IsBlockingMode(data));

            #region Internal

            bool IsBlockingMode(SkitPresentationData data)
            {
                // モード名は持ち主の定数を読む（生成と同じ値）
                // Read the owner's constant for the mode name (the same value the producer writes)
                return data.PresentationState.Mode == SkitPresentationData.BlockingMode;
            }

            #endregion
        }

    }
}
