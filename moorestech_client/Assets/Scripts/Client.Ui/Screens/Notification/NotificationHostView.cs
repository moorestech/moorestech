using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Notification.Parts;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行を Id で差分描画し、各行の生存を入場と退場の 2 本の tween で回して、退場が終わった行を書き口から除く。入力は奪わない
    /// Diff-renders notification rows by Id, runs each row's lifetime as an enter and an exit tween and removes rows through the write side when the exit ends; never takes input
    /// </summary>
    public sealed class NotificationHostView : VisualElement
    {
        private readonly INotificationListWriter _writer;
        private readonly INotificationTextLookup _textLookup;
        private readonly INotificationRowIcons _icons;
        private readonly Dictionary<long, ShownRow> _shownRows = new();
        private NotificationListViewModel _waitingForStyle;
        private NotificationRowTiming _timing;
        private IDisposable _languageSubscription;
        private float _stageScale = 1f;
        private float? _screenLeftPixels;

        public NotificationHostView(INotificationListWriter writer, INotificationTextLookup textLookup, INotificationRowIcons icons)
        {
            _writer = writer;
            _textLookup = textLookup;
            _icons = icons;
            name = "notification-host";
            AddToClassList("notification-host");
            pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<AttachToPanelEvent>(attachEvent => _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => ReapplyTexts()));
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Render(NotificationListViewModel viewModel)
        {
            // 尺はトークンから読むので、スタイル解決前の描画は解決後まで最新の 1 つを預かる
            // Durations come from tokens, so a render before style resolution keeps the latest one until it resolves
            // silent-ok: パネルに付いてスタイルが解決するまでの一時的な預かり / 解決が来ないまま配置まで進んだら（tss の @import 漏れ等）OnGeometryChanged が LogError を出す
            if (_timing == null)
            {
                _waitingForStyle = viewModel;
                return;
            }

            RemoveVanishedRows(viewModel);
            foreach (var row in viewModel.Rows) ShowRow(row);
        }

        private void ShowRow(NotificationRow row)
        {
            if (_shownRows.TryGetValue(row.Id, out var shown))
            {
                // silent-ok: Render は毎回全行を渡すので、生存尺が回り直していない行は触らない / 合流の再生は L1「合流で生存尺が回り直し…」が確かめる
                if (shown.LifetimeEpoch == row.LifetimeEpoch) return;

                // 合流で生存尺が回り直したら、文を差し替えて 2 本を頭から再生する（行の要素と位置は保つ）
                // When a merge restarts the lifetime, swap the text and replay both tweens from the head (keeping the element and position)
                shown.StopLifetime();
                shown.View.Show(row, TextOf(row), _icons);
                StartLifetime(shown, row);
                return;
            }

            // 新着は最上段へ（Web の column-reverse と同じ見え方）。登録してから生存を回し、除去の再入が必ず行を見つけるようにする
            // The newest goes on top (same look as the Web column-reverse); register before starting the lifetime so a re-entrant removal always finds the row
            var added = new ShownRow(new NotificationRowView());
            added.View.Show(row, TextOf(row), _icons);
            Insert(0, added.View);
            _shownRows.Add(row.Id, added);
            StartLifetime(added, row);
        }

        private void StartLifetime(ShownRow shown, NotificationRow row)
        {
            // 退場の終わりで行を書き口から除く。除去と退場が同じパネルの時計に乗る（除去の経路はこれだけ。D-2）
            // Remove the row through the write side at the exit end, so removal and exit share the panel clock (the only removal path, D-2)
            var handle = NotificationRowLifetime.Play(shown.View, _timing);
            var exitSubscription = handle.ExitCompleted.Subscribe(_ => _writer.RemoveNotification(row.Id));
            shown.SetLifetime(row.LifetimeEpoch, handle, exitSubscription);
        }

        private void RemoveVanishedRows(NotificationListViewModel viewModel)
        {
            var alive = new HashSet<long>(viewModel.Rows.Select(row => row.Id));
            foreach (var id in _shownRows.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                var shown = _shownRows[id];
                shown.StopLifetime();
                shown.View.RemoveFromHierarchy();
                _shownRows.Remove(id);
            }
        }

        private void ReapplyTexts()
        {
            // 内容名・アイテム名は解決済みの文字列で UiText に入るので、言語が変わったら行ごとに解き直す
            // Content and item names enter the UiText as resolved strings, so re-resolve every row when the language changes
            foreach (var shown in _shownRows.Values) shown.View.SetText(TextOf(shown.View.Row));
        }

        // 行の文の解決はこの 1 か所（新着・合流・言語の 3 経路が呼ぶ）
        // The single place that resolves a row's text (called by the new-row, merge and language paths)
        private UiText TextOf(NotificationRow row)
        {
            return NotificationText.Resolve(row.Content, _textLookup);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
        {
            _languageSubscription?.Dispose();

            // 外れると行の動きが止まり退場の終わりが来ない＝行が Model に残る。常駐のホストが外れるのは Dispose だけのはずなので、それ以外で起きたら開発者に知らせる
            // Detaching stops the row motions, so no exit end comes and rows stay in the model; a resident host should only detach on Dispose, so tell developers otherwise
            if (_shownRows.Count > 0) Debug.LogWarning($"[NotificationHostView] detached with {_shownRows.Count} rows; their exit motions stop and they will not be removed");
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 読めないトークンは Reader が LogError を出す。通知を届けるのを優先し、出入り無し（尺 0・ずれ 0）で出す
            // The reader logs unreadable tokens; delivering the notification comes first, so show it without enter/exit (0 durations, 0 shift)
            var hasEnter = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--notification-host-enter-duration", out var enter);
            var hasExit = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--notification-host-exit-duration", out var exit);
            var hasShift = UssCustomStyleReader.TryReadPixels(customStyle, "--notification-host-shift", out var shift);
            if (UssCustomStyleReader.TryReadPixels(customStyle, "--notification-host-screen-left", out var screenLeft)) _screenLeftPixels = screenLeft;
            ApplyScreenLeft();
            _timing = hasEnter && hasExit && hasShift
                ? new NotificationRowTiming(enter, exit, NotificationModel.DisplayMilliseconds, shift)
                : new NotificationRowTiming(0f, 0f, NotificationModel.DisplayMilliseconds, 0f);
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            var waiting = _waitingForStyle;
            _waitingForStyle = null;
            if (waiting != null) Render(waiting);
        }

        // 配置まで進んでも画面 USS の受け直しが来ていなければ、預かった通知は出ない。無音にしないため開発者に知らせる（尺が決まると購読を外す）
        // If layout ran but the screen USS custom properties never resolved, held notifications never show; tell developers (unsubscribed once durations are known)
        private void OnGeometryChanged(GeometryChangedEvent geometryEvent)
        {
            if (_timing == null && _waitingForStyle != null) Debug.LogError("[NotificationHostView] custom style not resolved after layout; is Notification.uss imported by GameUiTheme.tss and the notification-host class present?");
        }

        // 舞台の倍率を受け、左端を実画面の px に保つ（Web .host の left: 1rem は --ui-scale の外。中身は PanelSettings の倍率で舞台と同率）
        // Take the stage scale and keep the left edge in real-screen px (Web .host left: 1rem is outside --ui-scale; contents scale with the stage via PanelSettings)
        public void SetStageScale(float stageScale)
        {
            _stageScale = stageScale;
            ApplyScreenLeft();
        }

        private void ApplyScreenLeft()
        {
            // silent-ok: 画面 USS の解決前は 16px が未知なので置かない / 読めなければ UssCustomStyleReader が LogError、解決しないまま行が来れば OnGeometryChanged が LogError
            if (_screenLeftPixels == null) return;
            style.left = _screenLeftPixels.Value / _stageScale;
        }

        private sealed class ShownRow
        {
            public readonly NotificationRowView View;
            private NotificationRowLifetimeHandle _motion;
            private IDisposable _exitSubscription;
            public int LifetimeEpoch { get; private set; }

            public ShownRow(NotificationRowView view)
            {
                View = view;
            }

            public void SetLifetime(int lifetimeEpoch, NotificationRowLifetimeHandle motion, IDisposable exitSubscription)
            {
                LifetimeEpoch = lifetimeEpoch;
                _motion = motion;
                _exitSubscription = exitSubscription;
            }

            // 取り消した生存の退場の購読も切り、古い終わりが行を消す余地を残さない
            // Also drop the cancelled lifetime's exit subscription so a stale end can never remove the row
            public void StopLifetime()
            {
                _motion.Cancel();
                _exitSubscription.Dispose();
            }
        }
    }
}
