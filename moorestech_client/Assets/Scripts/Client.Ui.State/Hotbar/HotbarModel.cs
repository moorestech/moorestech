using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Network.API;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Routing;
using Client.Ui.State.SkitPresence;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの源を購読し、変化ごとに同期で組み直して配る（Web HotbarTopic と同じ源。設計書 A-57・前例 ModeHudModel）
    /// Subscribes to the hotbar sources and rebuilds synchronously on each change (same sources as Web HotbarTopic; design A-57, precedent ModeHudModel)
    /// </summary>
    public sealed class HotbarModel : IInitializable
    {
        private readonly ClientHotbarDatastore _datastore;
        private readonly PlacementTargetResolver _resolver;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IBlueprintThumbnailLookup _thumbnails;
        private readonly PlaceSystemStateController _placeSystem;
        private readonly UIStateControl _uiStateControl;
        private readonly UiRoutingState _routing;
        private readonly BlockingSkitPresence _blockingSkit;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ReactiveProperty<HotbarViewModel> _current;

        public IReadOnlyReactiveProperty<HotbarViewModel> Current => _current;

        public HotbarModel(ClientHotbarDatastore datastore, PlacementTargetResolver resolver, ClientBlueprintLibrary blueprintLibrary, IBlueprintThumbnailLookup thumbnails, PlaceSystemStateController placeSystem, UIStateControl uiStateControl, UiRoutingState routing, BlockingSkitPresence blockingSkit, IVanillaApiEvent vanillaApiEvent)
        {
            _datastore = datastore;
            _resolver = resolver;
            _blueprintLibrary = blueprintLibrary;
            _thumbnails = thumbnails;
            _placeSystem = placeSystem;
            _uiStateControl = uiStateControl;
            _routing = routing;
            _blockingSkit = blockingSkit;
            _vanillaApiEvent = vanillaApiEvent;
            _current = new ReactiveProperty<HotbarViewModel>(Build());
        }

        public void Initialize()
        {
            // 割当・設置対象（由来の枠）・UI 状態・画面・スキットの変化で組み直す
            // Rebuild on assignment, placement target (origin slot), UI state, screen and skit changes
            _datastore.OnAssignmentsChanged.Subscribe(_ => Rebuild());
            _placeSystem.OnTargetChanged.Subscribe(_ => Rebuild());
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _routing.Current.Subscribe(_ => Rebuild());
            _blockingSkit.IsBlocking.Subscribe(_ => Rebuild());

            // 絵と名前の出どころ（BP の改名・サムネイルの到着・言語）の変化も組み直す。値が変われば ViewModel の等価が外れて描き直る
            // Picture and name source changes (blueprint rename, thumbnail arrival, language) also rebuild; a changed value breaks view-model equality and redraws
            _blueprintLibrary.OnChanged.Subscribe(_ => Rebuild());
            _thumbnails.OnThumbnailChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // 研究の解放で未解決の枠が解けるようになる。解放状態の持ち主（ClientGameUnlockStateData）は ctor で同じタグを先に購読しているので、ここで読む時点で更新済み（計画 D-18）
            // An unlock makes unresolved slots resolvable; the unlock-state owner subscribes to the same tag in its ctor first, so it is already updated when read here (plan D-18)
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => Rebuild());
            Rebuild();
        }

        // 値が同じなら ReactiveProperty が流さない。BP の絵だけは参照の等価なので、BP の割当があれば組み直すたびに流れる（撮り直しの描き直しに要る）
        // ReactiveProperty skips equal values; a blueprint icon has reference equality, so a slot with a blueprint pushes on every rebuild (needed to redraw a recapture)
        private void Rebuild()
        {
            _current.Value = Build();
        }

        private HotbarViewModel Build()
        {
            var slots = new List<HotbarSlotViewModel>(_datastore.Assignments.Count);
            foreach (var assignedId in _datastore.Assignments) slots.Add(ResolveSlot(assignedId));
            return HotbarViewModelFactory.Create(slots, _placeSystem.CurrentOrigin, _uiStateControl.CurrentState, _routing.Current.Value.Screen, _blockingSkit.IsBlocking.Value);

            #region Internal

            // 未割当だけが空き枠。割当があって解けない枠は外せる未解決として残す（Web HotbarTopic.ResolveSlotDto）
            // Only unassigned slots are empty; an assigned but unresolvable slot stays as a clearable unresolved one (Web HotbarTopic.ResolveSlotDto)
            HotbarSlotViewModel ResolveSlot(Guid assignedId)
            {
                if (assignedId == Guid.Empty) return HotbarEmptySlot.Instance;
                if (!_resolver.TryResolve(assignedId, out var target)) return new HotbarUnresolvedSlot(assignedId);

                // 表示名は今の言語の名（S2-mode-tooltip D-8）、絵の出どころはビルドメニューと同じ解き方
                // The display name is in the current language (S2-mode-tooltip D-8); the picture source resolves as in the build menu
                return new HotbarAssignedSlot(target.Id, target.DisplayName, PlacementTargetIconResolver.Resolve(target, _thumbnails));
            }

            #endregion
        }
    }
}
