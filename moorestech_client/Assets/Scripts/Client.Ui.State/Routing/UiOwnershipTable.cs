using System;
using System.Collections.Generic;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 画面×描画面ごとの描画担当の表
    /// Table of who draws each screen-surface pair
    /// </summary>
    public class UiOwnershipTable
    {
        // 所有表の行。後続レーンは面を足したらここへ全画面ぶんの行を足し、切替 PR で Web→GameUi に書き換える
        // Ownership rows; later lanes add a row per screen for each new surface and flip Web→GameUi in the cutover PR
        private static readonly (UiScreen screen, UiSurface surface, UiSurfaceOwner owner)[] Rows =
        {
            (UiScreen.None, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.PlayerInventory, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.SubInventory, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.ResearchTree, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.BuildMenu, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.ChallengeList, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.PauseMenu, UiSurface.MainPanel, UiSurfaceOwner.GameUi),
            (UiScreen.TrainHud, UiSurface.MainPanel, UiSurfaceOwner.Web),
            (UiScreen.TrainPause, UiSurface.MainPanel, UiSurfaceOwner.GameUi),
            (UiScreen.SkitPause, UiSurface.MainPanel, UiSurfaceOwner.GameUi),
        };

        private readonly Dictionary<(UiScreen, UiSurface), UiSurfaceOwner> _owners = new();

        public UiOwnershipTable()
        {
            // 全組がちょうど 1 行。欠け・重複は起動時に止める
            // Exactly one row per pair; missing or duplicated pairs stop at startup
            foreach (var row in Rows)
            {
                if (!_owners.TryAdd((row.screen, row.surface), row.owner)) throw new InvalidOperationException($"[UiOwnershipTable] duplicated row {row.screen}/{row.surface}");
            }
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            foreach (UiSurface surface in Enum.GetValues(typeof(UiSurface)))
            {
                if (!_owners.ContainsKey((screen, surface))) throw new InvalidOperationException($"[UiOwnershipTable] missing row {screen}/{surface}");
            }
        }

        public UiSurfaceOwner GetOwner(UiScreen screen, UiSurface surface)
        {
            var owner = _owners[(screen, surface)];
#if UNITY_EDITOR
            if (UiOwnershipEditorOverride.TryGet(screen, surface, out var overridden)) owner = overridden;
#endif
            return owner;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor 限定で所有表の行を一時的に上書きする
    /// Editor-only temporary override of ownership rows
    /// </summary>
    public static class UiOwnershipEditorOverride
    {
        private static readonly Dictionary<(UiScreen, UiSurface), UiSurfaceOwner> Overrides = new();
        private static readonly UniRx.Subject<UniRx.Unit> OnChangedSubject = new();

        // 上書きの変化。ルータが購読して次の state 遷移を待たずに解決し直す
        // Override changes; the router subscribes and re-resolves without waiting for the next state transition
        public static IObservable<UniRx.Unit> OnChanged => OnChangedSubject;

        public static void Set(UiScreen screen, UiSurface surface, UiSurfaceOwner owner)
        {
            Overrides[(screen, surface)] = owner;
            UnityEngine.Debug.Log($"[UiOwnershipEditorOverride] set {screen}/{surface} -> {owner}");
            OnChangedSubject.OnNext(UniRx.Unit.Default);
        }

        public static void Clear()
        {
            Overrides.Clear();
            UnityEngine.Debug.Log("[UiOwnershipEditorOverride] cleared all overrides");
            OnChangedSubject.OnNext(UniRx.Unit.Default);
        }

        internal static bool TryGet(UiScreen screen, UiSurface surface, out UiSurfaceOwner owner)
        {
            return Overrides.TryGetValue((screen, surface), out owner);
        }
    }
#endif
}
