using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.Hotbar
{
    /// <summary>
    /// local_player.hotbar トピック: ホットバー9枠の割当表示情報とselectedSlotをpush
    /// local_player.hotbar topic: pushes the hotbar's 9 assignment slots' display info and selectedSlot
    /// </summary>
    public class HotbarTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.hotbar";

        private readonly WebSocketHub _hub;
        private readonly ClientHotbarDatastore _clientHotbarDatastore;
        private readonly HotbarPlacementTargetResolver _hotbarPlacementTargetResolver;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IDisposable _datastoreSubscription;
        private readonly IDisposable _librarySubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public HotbarTopic(WebSocketHub hub, ClientHotbarDatastore clientHotbarDatastore, HotbarPlacementTargetResolver hotbarPlacementTargetResolver, ClientBlueprintLibrary blueprintLibrary)
        {
            _hub = hub;
            _clientHotbarDatastore = clientHotbarDatastore;
            _hotbarPlacementTargetResolver = hotbarPlacementTargetResolver;
            _blueprintLibrary = blueprintLibrary;

            // 割当/選択枠の変更と、解決先であるBPライブラリの変更の両方で再配信する
            // Republish on assignment/selected-slot changes and on blueprint-library changes (the resolution source)
            _datastoreSubscription = _clientHotbarDatastore.OnChanged.Subscribe(_ => SchedulePublish());
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _datastoreSubscription.Dispose();
            _librarySubscription.Dispose();
        }

        // INFRA-7 デバウンス規約: 同フレーム多発でもフレーム末の最終状態だけ配信する（前例 BuildMenuTopic）
        // INFRA-7 debounce rule: publish only the frame-end final state even on same-frame bursts (precedent: BuildMenuTopic)
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }

            #endregion
        }

        private string BuildJson()
        {
            var slots = new List<HotbarSlotDto>(_clientHotbarDatastore.Assignments.Count);
            foreach (var id in _clientHotbarDatastore.Assignments)
            {
                slots.Add(ResolveSlotDto(id));
            }

            var dto = new HotbarTopicDto
            {
                Slots = slots,
                SelectedSlot = _clientHotbarDatastore.SelectedSlot,
            };
            return WebUiJson.Serialize(dto);

            #region Internal

            // 未割当・未解決（未解放/削除済みBP等）はnullスロットとして配信する
            // Unassigned or unresolved (locked/deleted blueprint, etc.) slots ship as null
            HotbarSlotDto ResolveSlotDto(Guid id)
            {
                if (id == Guid.Empty) return null;
                if (!_hotbarPlacementTargetResolver.TryResolve(id, out var entry)) return null;

                var target = PlacementTargetFactory.Create(entry);
                return new HotbarSlotDto
                {
                    Id = target.Id.ToString("D"),
                    Kind = BuildMenuEntryDtoFactory.ResolveKind(target.Kind),
                    Label = target.DisplayName,
                    IconUrl = BuildMenuEntryDtoFactory.ResolveIconUrl(target),
                };
            }

            #endregion
        }
    }
}
