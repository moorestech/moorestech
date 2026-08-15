using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Cysharp.Threading.Tasks;
using Game.PlacementTarget;
using Server.Event.EventReceive;
using UniRx;

namespace Client.WebUiHost.Game.Topics.Hotbar
{
    /// <summary>
    /// local_player.hotbar: 9枠情報をpush
    /// local_player.hotbar topic: pushes the hotbar's 9 assignment slots' display info and selectedSlot
    /// </summary>
    public class HotbarTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.hotbar";

        // 由来がホットバー以外の未選択値
        // The unselected value published when the origin is not a hotbar slot (menu, eyedropper, or nothing)
        private const int UnselectedSlot = -1;

        // 割当はあるが解決できない枠（未解放・削除済みBP）のkind。webはこれを使用不可の割当として描く
        // The kind for an assigned slot that cannot be resolved (locked target, deleted blueprint); the web draws it as an unusable assignment
        private const string UnresolvedKind = "unresolved";

        private readonly WebSocketHub _hub;
        private readonly ClientHotbarDatastore _clientHotbarDatastore;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly IDisposable _datastoreSubscription;
        private readonly IDisposable _librarySubscription;
        private readonly IDisposable _targetSubscription;
        private readonly IDisposable _unlockSubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public HotbarTopic(WebSocketHub hub, ClientHotbarDatastore clientHotbarDatastore, PlacementTargetResolver placementTargetResolver, ClientBlueprintLibrary blueprintLibrary, PlaceSystemStateController placeSystemStateController)
        {
            _hub = hub;
            _clientHotbarDatastore = clientHotbarDatastore;
            _placementTargetResolver = placementTargetResolver;
            _blueprintLibrary = blueprintLibrary;
            _placeSystemStateController = placeSystemStateController;

            // 割当の変更・解決先であるBPライブラリの変更・由来枠を含む設置対象の変更で再配信する
            // Republish on assignment changes, on blueprint-library changes (the resolution source), and on placement-target changes that carry the origin
            _datastoreSubscription = _clientHotbarDatastore.OnAssignmentsChanged.Subscribe(_ => SchedulePublish());
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
            _targetSubscription = _placeSystemStateController.OnTargetChanged.Subscribe(_ => SchedulePublish());

            // 研究で解放されると未解決枠が解決可能へ変わるため再配信する（前例 MachineRecipesTopic）
            // An unlock turns unresolved slots resolvable, so republish on it (precedent: MachineRecipesTopic)
            _unlockSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => SchedulePublish());
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
            _targetSubscription.Dispose();
            _unlockSubscription.Dispose();
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

            // ハイライトする枠は設置対象の所有者が持つ由来から導く（表示と実対象が構造的に一致する）
            // The highlighted slot derives from the origin owned by the placement target's owner, so display and target always agree
            var dto = new HotbarTopicDto
            {
                Slots = slots,
                SelectedSlot = _placeSystemStateController.CurrentOrigin.TryGetHotbarSlot(out var originSlot) ? originSlot : UnselectedSlot,
            };
            return WebUiJson.Serialize(dto);

            #region Internal

            // 未割当だけがnull枠。割当済みで解決できない枠はIdだけのunresolvedとして配信し、webが外せる割当として扱えるようにする
            // Only unassigned slots are null; an assigned but unresolvable slot ships as unresolved with just its id so the web can still clear it
            HotbarSlotDto ResolveSlotDto(Guid id)
            {
                if (id == Guid.Empty) return null;
                if (!_placementTargetResolver.TryResolve(id, out var target)) return new HotbarSlotDto { Id = id.ToString("D"), Kind = UnresolvedKind };

                return new HotbarSlotDto
                {
                    Id = target.Id.ToString("D"),
                    Kind = BuildMenuEntryDtoFactory.ResolveKind(target.Kind),
                    // マスタ由来名はweb側がGuid導出キーで解決する。ユーザー命名のBPだけ原文を運ぶ（前例 BuildMenuEntryDtoFactory）
                    // The web resolves master-derived names by GUID-keyed lookup; only user-named blueprints carry their raw text (precedent: BuildMenuEntryDtoFactory)
                    Label = target.Kind == PlacementTargetKind.Blueprint ? target.DisplayName : null,
                    IconUrl = BuildMenuEntryDtoFactory.ResolveIconUrl(target),
                };
            }

            #endregion
        }
    }
}
