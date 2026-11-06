using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.BuildMenu.Session;
using UniRx;
using UnityEngine;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// 1 本スクロールの現在地・ジャンプ固定・末尾スペーサ・位置復元を決める（Web useBuildMenuCategoryScroll の計算部分の写し）
    /// Decides the current category, jump pin, trailing spacer and restore of the single scroll (port of the math in the Web useBuildMenuCategoryScroll)
    /// </summary>
    public sealed class BuildMenuCategoryScrollTracker
    {
        public IReadOnlyReactiveProperty<Guid?> ActiveCategory => _activeCategory;
        public IReadOnlyReactiveProperty<float> SpacerHeight => _spacerHeight;

        private readonly ReactiveProperty<Guid?> _activeCategory = new();
        private readonly ReactiveProperty<float> _spacerHeight = new(0f);
        private readonly BuildMenuSessionStore _session;
        private IReadOnlyList<Guid> _visibleCategories = Array.Empty<Guid>();
        private IBuildMenuScrollViewport _viewport;

        // ジャンプ中はハイライトを目標に固定し、到達か操作でスクロール追従へ戻す
        // While jumping the highlight is pinned to the target; arrival or user input releases it
        private BuildMenuJumpPin? _jumpPin;

        // 保存位置を代入済みか。視口を張り替えたら偽へ戻す
        // Whether the saved position was applied; reset on a viewport swap
        private bool _scrollRestored;

        public BuildMenuCategoryScrollTracker(BuildMenuSessionStore session)
        {
            _session = session;
        }

        public void AttachViewport(IBuildMenuScrollViewport viewport)
        {
            // 張り替え前の実効位置を保存し、新しい視口で復元をやり直す
            // Persist the outgoing viewport's position and redo the restore on the new one
            if (_viewport != null) _session.SetScrollTop(_viewport.ScrollTop);
            _viewport = viewport;
            _scrollRestored = false;
            SyncLayout();
        }

        public void SetVisibleCategories(IReadOnlyList<Guid> categoryGuids)
        {
            // 同じ並びの渡し直しではジャンプの固定を保つ
            // Passing the same sequence again keeps the jump pin
            if (_visibleCategories.SequenceEqual(categoryGuids)) return;
            _visibleCategories = categoryGuids.ToArray();
            _jumpPin = null;
            SyncLayout();
        }

        public void JumpTo(Guid categoryGuid)
        {
            if (_viewport == null || !_viewport.TryGetHeadingTop(categoryGuid, out var headingTop))
            {
                Debug.LogWarning($"[BuildMenuCategoryScroll] jump to {categoryGuid} ignored: the viewport or its heading is not attached");
                return;
            }
            _activeCategory.Value = categoryGuid;
            PinOrSettleJump(categoryGuid, ReachableTop(headingTop));
        }

        public void OnScrolled(float scrollTop)
        {
            _session.SetScrollTop(scrollTop);
            if (_jumpPin is { } pin)
            {
                // 未到達の間は固定を保ち、到達したら追従ではなく目標自身を確定させる
                // Keep the pin until arrival, then settle on the target itself rather than on scroll-spy
                if (!BuildMenuScrollSpy.IsJumpSettled(scrollTop, pin.Top)) return;
                SettleOnJumpTarget(pin.CategoryGuid);
                return;
            }
            RecomputeActiveCategory(scrollTop);
        }

        // ホイール・押下・キー入力でジャンプの固定をすぐ解く
        // Wheel, press or key input releases the jump pin immediately
        public void OnUserIntervened()
        {
            // silent-ok: 固定が無いときの解除は冪等 / 現在地は ActiveCategory の購読者が見る
            // Releasing with no pin is idempotent; ActiveCategory subscribers see the current category
            if (_jumpPin == null) return;
            _jumpPin = null;
            RecomputeActiveCategory(_viewport.ScrollTop);
        }

        // 視口・見出し・群・スペーサの寸法が変わったら View が呼ぶ
        // The view calls this whenever the viewport, a heading, a group or the spacer changes size
        public void OnLayoutChanged() => SyncLayout();

        public void PersistScrollPosition()
        {
            _session.SetScrollTop(_viewport.ScrollTop);
        }

        private void SyncLayout()
        {
            // silent-ok: 視口の装着前は測れない / AttachViewport が同じ処理をやり直す
            // Nothing is measurable before a viewport attaches; AttachViewport reruns this
            if (_viewport == null) return;
            var spacer = RemeasureSpacer();
            if (spacer == BuildMenuSpacerMeasure.Settled) RestoreScrollOnce();
            if (_jumpPin is not { } pin)
            {
                RecomputeActiveCategory(_viewport.ScrollTop);
                return;
            }

            // 反映前の scrollHeight で引き直すと末尾ジャンプが手前で到達扱いになるので、反映後の通知を待つ
            // Re-targeting on the pre-apply scrollHeight settles a trailing jump short, so wait for the applied notification
            if (spacer == BuildMenuSpacerMeasure.Changed) return;
            RetargetJump(pin);
        }

        private BuildMenuSpacerMeasure RemeasureSpacer()
        {
            // 末尾群が未配置の間は実測できない。0 と確定させると復元位置まで 0 へ潰れる
            // The last group is unmeasurable until laid out; settling it as 0 would flatten the restore target too
            if (_visibleCategories.Count == 0) return BuildMenuSpacerMeasure.Unmeasurable;
            var lastCategory = _visibleCategories[_visibleCategories.Count - 1];
            if (!_viewport.TryGetGroupHeight(lastCategory, out var lastGroupHeight)) return BuildMenuSpacerMeasure.Unmeasurable;

            var desired = BuildMenuScrollSpy.TrailingSpacerHeight(_viewport.ClientHeight, lastGroupHeight);
            if (desired == _spacerHeight.Value) return BuildMenuSpacerMeasure.Settled;
            _spacerHeight.Value = desired;
            return BuildMenuSpacerMeasure.Changed;
        }

        private void RestoreScrollOnce()
        {
            if (_scrollRestored) return;
            _scrollRestored = true;
            _viewport.SetScrollTopImmediately(_session.Current.ScrollTop);

            // クランプ後の実効値へ保存を揃える
            // Realign the store with the clamped effective value
            _session.SetScrollTop(_viewport.ScrollTop);
        }

        private void RetargetJump(BuildMenuJumpPin pin)
        {
            if (!_viewport.TryGetHeadingTop(pin.CategoryGuid, out var headingTop))
            {
                Debug.LogWarning($"[BuildMenuCategoryScroll] jump target {pin.CategoryGuid} lost its heading; the pin waits for user input or a visible-set change");
                return;
            }
            var top = ReachableTop(headingTop);
            if (top == pin.Top) return;
            PinOrSettleJump(pin.CategoryGuid, top);
        }

        // スペーサ込みでも届かないときは最大スクロール位置を目標にし、到達判定を成立させる
        // When even the spacer cannot reach, target the max scroll so the settle check can succeed
        private float ReachableTop(float headingTop) => Math.Min(headingTop, _viewport.ScrollHeight - _viewport.ClientHeight);

        // 目標位置にすでに居れば目標自身で確定し、居なければ固定してスムーズに動かす
        // Settle on the target if already there, otherwise pin it and scroll smoothly
        private void PinOrSettleJump(Guid categoryGuid, float top)
        {
            if (BuildMenuScrollSpy.IsJumpSettled(_viewport.ScrollTop, top))
            {
                SettleOnJumpTarget(categoryGuid);
                return;
            }
            _jumpPin = new BuildMenuJumpPin(categoryGuid, top);
            _viewport.ScrollSmoothlyTo(top);
        }

        // 到達時は scroll-spy ではなく目標自身を現在地にし、固定を解く
        // On arrival the target itself becomes current (not scroll-spy) and the pin is released
        private void SettleOnJumpTarget(Guid categoryGuid)
        {
            _jumpPin = null;
            _activeCategory.Value = categoryGuid;
        }

        private void RecomputeActiveCategory(float scrollTop)
        {
            var offsets = new List<CategoryHeadingOffset>();
            foreach (var categoryGuid in _visibleCategories)
            {
                if (_viewport.TryGetHeadingTop(categoryGuid, out var top)) offsets.Add(new CategoryHeadingOffset(categoryGuid, top));
            }
            _activeCategory.Value = BuildMenuScrollSpy.ActiveCategoryAtScroll(offsets, scrollTop);
        }
    }
}
