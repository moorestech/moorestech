using System;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraphのハッシュ整合性を定期的に検証する
    ///     Periodically verifies RailGraph hash consistency between client/server
    /// </summary>
    public sealed class RailGraphHashVerifier : IInitializable, IDisposable
    {
        private const float VerificationIntervalSeconds = 30f;

        private readonly RailGraphClientCache _cache;
        private readonly RailGraphSnapshotApplier _snapshotApplier;
        private CancellationTokenSource _cancellation;

        public RailGraphHashVerifier(RailGraphClientCache cache, RailGraphSnapshotApplier snapshotApplier)
        {
            _cache = cache;
            _snapshotApplier = snapshotApplier;
        }

        public void Initialize()
        {
            _cancellation = new CancellationTokenSource();
            VerifyLoop().Forget();
        }

        public void Dispose()
        {
            if (_cancellation == null) return;
            _cancellation.Cancel();
            _cancellation.Dispose();
            _cancellation = null;
        }

        private async UniTaskVoid VerifyLoop()
        {
            if (_cancellation == null)
            {
                return;
            }

            var token = _cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await VerifyOnceAsync(token);
                    await UniTask.Delay(TimeSpan.FromSeconds(VerificationIntervalSeconds), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RailGraphHashVerifier] Verification failed: {ex}");
                }
            }
        }

        private async UniTask VerifyOnceAsync(CancellationToken ct)
        {
            var api = ClientContext.VanillaApi?.Response;
            if (api == null)
            {
                return;
            }

            var clientHash = _cache.ComputeCurrentHash();
            var response = await api.GetRailGraphHash(clientHash, ct);
            if (response == null)
            {
                return;
            }

            if (!response.NeedsResync)
            {
                _cache.OverrideTick(response.ServerTick);
                return;
            }

            Debug.LogWarning($"[RailGraphHashVerifier] Hash mismatch detected. client={clientHash}, server={response.ServerHash}. Applying snapshot.");
            if (response.Snapshot != null)
            {
                _snapshotApplier.ApplySnapshot(response.Snapshot);
            }
            else
            {
                Debug.LogWarning("[RailGraphHashVerifier] Snapshot was not provided despite mismatch.");
            }
        }
    }
}