using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// 保留モーダル要求を持ち応答で解決
    /// Holds the pending modal request, resolved by reply
    /// </summary>
    public class ModalRequestService : IInitializable
    {
        // 保留中の要求。なしはnull
        // Pending request, or null
        public ModalRequest Pending { get; private set; }

        // Pending が変化したら発火する（topic 再配信のトリガ）
        // Fires when Pending changes, triggering topic republish
        public IObservable<Unit> OnPendingChanged => _onPendingChanged;
        private readonly Subject<Unit> _onPendingChanged = new();

        private UniTaskCompletionSource<(ModalResult result, string text)> _pendingSource;
        private int _nextId;
        // 最後に発行した要求ID。保留が解けても残し、置き換えられた旧要求を見分ける
        // The last issued request id; kept after the pending request resolves to tell replaced requests apart
        private string _latestIssuedId;
        // 保留中の要求を開いた側の寿命への登録。保留が外れるときに必ず外す
        // Registration on the opener's lifetime for the pending request; always dropped when the pending request is detached
        private CancellationTokenRegistration _pendingOwnerRegistration;

        public void Initialize()
        {
            // 保留要求の寿命はゲーム寿命なので、描く側の停止でなくゲーム終了で取り消す
            // Pending requests live as long as the game, so cancel them at game shutdown rather than when a renderer stops
            GameShutdownEvent.OnGameShutdown.Subscribe(_ => CancelPendingRequest());
        }

        // モーダルを出す。開いた側の寿命（ownerLifetime）が尽きたら、この要求だけを閉じる
        // Show a modal; when the opener's lifetime (ownerLifetime) ends, only this request is closed
        public ModalRequestHandle RequestModal(string title, string message, string buttonText, ModalVariant variant, CancellationToken ownerLifetime)
        {
            return RequestCore(title, message, buttonText, variant, false, Array.Empty<ModalTableRow>(), ownerLifetime);
        }

        // 入力付きモーダルを出す（取り消しのとき text は null）
        // Show an input modal (text is null on cancel)
        public ModalRequestHandle RequestInputModal(string title, string message, string buttonText, CancellationToken ownerLifetime)
        {
            return RequestCore(title, message, buttonText, ModalVariant.Confirm, true, Array.Empty<ModalTableRow>(), ownerLifetime);
        }

        // 項目名と値の行を並べたモーダルを出す（入力欄なし）
        // Show a modal listing label-and-values rows (no input field)
        public ModalRequestHandle RequestTableModal(string title, string message, IReadOnlyList<ModalTableRow> rows, string buttonText, CancellationToken ownerLifetime)
        {
            // 空の行は表のモーダルにならず確認ダイアログへ黙って落ちるので入口で拒む
            // Empty rows would silently fall back to the confirm dialog, so reject them at the entrance
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (rows.Count == 0) throw new ArgumentException("表のモーダルには1行以上が要る / A table modal needs at least one row", nameof(rows));
            return RequestCore(title, message, buttonText, ModalVariant.Confirm, false, rows, ownerLifetime);
        }

        private ModalRequestHandle RequestCore(string title, string message, string buttonText, ModalVariant variant, bool requiresInput, IReadOnlyList<ModalTableRow> rows, CancellationToken ownerLifetime)
        {
            _nextId++;
            var requestId = _nextId.ToString();

            // 開いた側がもう居ない要求は出さない。今の保留も置き換えない
            // A request whose opener is already gone is not shown and does not replace the pending one
            if (ownerLifetime.IsCancellationRequested)
            {
                Debug.LogWarning($"[ModalRequestService] request {requestId} ignored: the owner's lifetime has already ended");
                return new ModalRequestHandle(this, requestId, UniTask.FromResult((ModalResult.Cancel, (string)null)));
            }

            // 最新要求の状態を先に確定し、既存要求は再入しても上書きしない順序で cancel 解決する
            // Commit the latest request state first, then cancel the previous request without reentrant overwrites
            var previousSource = DetachPending();
            _latestIssuedId = requestId;
            var source = new UniTaskCompletionSource<(ModalResult result, string text)>();
            _pendingSource = source;

            Pending = new ModalRequest
            {
                Id = requestId,
                Title = title,
                Message = message,
                ButtonText = buttonText,
                Variant = variant,
                RequiresInput = requiresInput,
                Rows = rows,
            };

            // 通知より先に登録する。通知の中で差し替えられたとき、差し替えた側の登録を上書きしないため
            // Register before notifying so that a replacement made inside the notification keeps its own registration
            _pendingOwnerRegistration = ownerLifetime.Register(() => Respond(requestId, ModalResult.Cancel, null));
            _onPendingChanged.OnNext(Unit.Default);

            previousSource?.TrySetResult((ModalResult.Cancel, null));
            return new ModalRequestHandle(this, requestId, source.Task);
        }

        // 最後に出した要求か。置き換えで cancel 解決された旧要求を見分ける
        // Whether this is the latest issued request; tells apart old requests cancelled by replacement
        public bool IsLatestRequest(string id)
        {
            return id == _latestIssuedId;
        }

        // 応答・取り消しの入口。id 不一致は古い応答なので無視する
        // Entrance for replies and cancellations; ignore id mismatches as stale responses
        public bool Respond(string id, ModalResult result, string text)
        {
            // silent-ok: 遅延した応答は現要求を変更しない / action呼出元がfalseを受け取る
            // Stale replies leave the current request intact; the action caller receives false.
            if (_pendingSource == null || id == null || id != Pending.Id) return false;

            var source = DetachPending();
            _onPendingChanged.OnNext(Unit.Default);

            source.TrySetResult((result, text));
            return true;
        }

        private void CancelPendingRequest()
        {
            // silent-ok: 保留無しの終了は取り消すものが無い / ゲーム終了の経路でしか呼ばれない
            if (Pending == null) return;
            Respond(Pending.Id, ModalResult.Cancel, null);
        }

        // 保留を外して待ちを返す。持ち主の寿命の登録もここで外し、後の発火が別の要求を閉じないようにする
        // Detach the pending request and return its wait; the owner-lifetime registration is dropped here so a later firing cannot close another request
        private UniTaskCompletionSource<(ModalResult result, string text)> DetachPending()
        {
            var source = _pendingSource;
            _pendingSource = null;
            Pending = null;
            _pendingOwnerRegistration.Dispose();
            _pendingOwnerRegistration = default;
            return source;
        }
    }
}
