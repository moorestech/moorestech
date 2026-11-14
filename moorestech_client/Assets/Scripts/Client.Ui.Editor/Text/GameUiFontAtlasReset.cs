using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Client.Ui.Editor.Text
{
    /// <summary>
    /// Editor のドメインを読み込むたびに GameUi の動的フォントの atlas と字の表を空にする（Editor だけ）
    /// Empties the GameUi dynamic font atlases and glyph tables every time the Editor domain loads (Editor only)
    /// </summary>
    /// <remarks>
    /// Editor では字の表だけが作り直され atlas の画素が残ることがあり、新しい字が古い字の上に詰められて、字の四角の外の古い字の欠片が倍率 1.5 の字や影の縁に点・短い線として写る（I-fix5 Task 2 の実測: 和文の atlas が 306 字で 5 枚、空にすると点が消える）。
    /// ビルドは ClearDynamicDataOnBuild で空の atlas から始まるので、Editor もドメインの読み込み（コンパイル後・PlayMode に入るとき）ごとに同じ空の状態から始める。
    /// 空にするのはドメインの読み込みの直後だけにする。2 枚目以降の atlas は空にすると破棄され、同じドメインで先に組まれた字の描画の情報が破棄済みの atlas の material を引いたまま残り、次に同じ字を描くと MeshGenerator.DrawText が NullReferenceException で落ちて以後の UI の描画が連鎖して壊れる（bd moorestech-albof.68 の実測）。読み込みの直後はまだ字の描画の情報が無いので安全。
    /// In the Editor only the glyph table can get rebuilt while the atlas pixels stay, so new glyphs pack over stale ones and the stale fragments outside a glyph's box show as dots and short lines around glyphs and shadows at scale 1.5 (I-fix5 Task 2: the CJK atlas held 306 glyphs over 5 pages and the dots vanished once emptied).
    /// Builds start from an empty atlas through ClearDynamicDataOnBuild, so the Editor also starts from the same empty state on every domain load (after a compile and on entering PlayMode).
    /// Emptying happens only right after a domain load: emptying destroys the second and later atlas pages, text layouts built earlier in the same domain keep the destroyed pages' materials, and drawing the same text again throws a NullReferenceException in MeshGenerator.DrawText that breaks all later UI drawing (measured in bd moorestech-albof.68). Right after a load no text layout exists yet, so it is safe.
    /// </remarks>
    [InitializeOnLoad]
    public static class GameUiFontAtlasReset
    {
        private const string FontFolder = "Assets/UI Toolkit/GameUi/Fonts";

        static GameUiFontAtlasReset()
        {
            // 取り込み用の子プロセスは字を描かないので資産に触れない
            // Asset import worker processes never draw text, so they leave the assets alone
            if (AssetDatabase.IsAssetImportWorkerProcess()) return; // silent-ok: 取り込み用の子プロセスは字を描かない / 本体の Editor のドメインの読み込みで空にする
            ClearGameUiFontAtlases();

            #region Internal

            void ClearGameUiFontAtlases()
            {
                // 字の表と atlas を一緒に空にする。字が残っていた資産があったときだけ件数を Console に出す
                // Empty the glyph tables together with the atlases; report to the Console only when some asset still held glyphs
                var guids = AssetDatabase.FindAssets("t:FontAsset", new[] { FontFolder });
                var clearedGlyphs = 0;
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                    // 初回の取り込みの途中など、まだ読めない資産は空にできない。読めた回に空になる
                    // An asset not loadable yet (e.g. mid first import) cannot be emptied; it is emptied on a later load
                    if (fontAsset == null)
                    {
                        Debug.LogWarning($"[GameUiFontAtlasReset] skipped {path}: the font asset could not be loaded during the domain load");
                        continue;
                    }
                    clearedGlyphs += fontAsset.glyphTable.Count;
                    fontAsset.ClearFontAssetData(true);
                }
                if (clearedGlyphs == 0) return; // silent-ok: 空の atlas を空にしただけで状態は変わらない / 字が残っていた回は下の行が Console に出る
                Debug.Log($"[GameUiFontAtlasReset] cleared {clearedGlyphs} glyphs from {guids.Length} GameUi font atlases");
            }

            #endregion
        }
    }
}
