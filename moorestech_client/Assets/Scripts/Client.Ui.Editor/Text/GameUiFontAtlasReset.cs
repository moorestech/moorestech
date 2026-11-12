using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Client.Ui.Editor.Text
{
    /// <summary>
    /// PlayMode に入る前に GameUi の動的フォントの atlas と字の表を空にする（Editor だけ）
    /// Empties the GameUi dynamic font atlases and glyph tables before entering PlayMode (Editor only)
    /// </summary>
    /// <remarks>
    /// Editor では字の表だけが作り直され atlas の画素が残ることがあり、新しい字が古い字の上に詰められて、字の四角の外の古い字の欠片が倍率 1.5 の字や影の縁に点・短い線として写る（I-fix5 Task 2 の実測: 和文の atlas が 306 字で 5 枚、空にすると点が消える）。
    /// ビルドは ClearDynamicDataOnBuild で空の atlas から始まるので、Editor の PlayMode だけを毎回同じ空の状態から始める。
    /// In the Editor only the glyph table can get rebuilt while the atlas pixels stay, so new glyphs pack over stale ones and the stale fragments outside a glyph's box show as dots and short lines around glyphs and shadows at scale 1.5 (I-fix5 Task 2: the CJK atlas held 306 glyphs over 5 pages and the dots vanished once emptied).
    /// Builds start from an empty atlas through ClearDynamicDataOnBuild, so each Editor PlayMode also starts from the same empty state.
    /// EditMode の撮影（UiRuntimePanelCapture）も描く前に同じく空にする。
    /// EditMode captures (UiRuntimePanelCapture) empty them the same way before drawing.
    /// </remarks>
    [InitializeOnLoad]
    public static class GameUiFontAtlasReset
    {
        private const string FontFolder = "Assets/UI Toolkit/GameUi/Fonts";

        static GameUiFontAtlasReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            ClearGameUiFontAtlases();
        }

        internal static void ClearGameUiFontAtlases()
        {
            // 字の表と atlas を一緒に空にする。字が残っていた資産があったときだけ件数を Console に出す
            // Empty the glyph tables together with the atlases; report to the Console only when some asset still held glyphs
            var guids = AssetDatabase.FindAssets("t:FontAsset", new[] { FontFolder });
            var clearedGlyphs = 0;
            foreach (var guid in guids)
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                clearedGlyphs += fontAsset.glyphTable.Count;
                fontAsset.ClearFontAssetData(true);
            }
            if (clearedGlyphs == 0) return; // silent-ok: 空の atlas を空にしただけで状態は変わらない / 字が残っていた回は下の行が Console に出る
            Debug.Log($"[GameUiFontAtlasReset] cleared {clearedGlyphs} glyphs from {guids.Length} GameUi font atlases");
        }
    }
}
