// GameUi の太さ違いの SDF FontAsset 4 つを作る。uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)" で実行する
// 元の Font と fallback の FontAsset が全部読めてから作り、4 つとも作れてから古い asset を置き換える（途中で失敗しても既存の FontAsset を消さない）
// Creates the four weight-variant GameUi SDF FontAssets; run with uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)"
// Creation starts only after every source Font and fallback FontAsset loads, and old assets are replaced only after all four are created (a failure never deletes the existing FontAssets)
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
const string Folder = "Assets/UI Toolkit/GameUi/Fonts";
const string FontFolder = "Assets/Dependencies/Font";
var specs = new[]
{
    (source: $"{FontFolder}/NotoSansKR-Bold.ttf", name: "GameUiKoreanBold SDF"),
    (source: $"{FontFolder}/NotoSansJP-Bold.ttf", name: "GameUiJapaneseBold SDF"),
    (source: $"{FontFolder}/NotoSans-Bold.ttf", name: "GameUiLatinBold SDF"),
    (source: $"{FontFolder}/NotoSans-Medium.ttf", name: "GameUiLatinMedium SDF"),
};

// 元の Font と、Medium の fallback にする段階 1 の JP/KR を先に全部読む。欠けていれば何も消さずに止める
// Load every source Font and the stage-1 JP/KR fallbacks first; stop without deleting anything if one is missing
var fonts = new List<Font>();
foreach (var spec in specs)
{
    var font = AssetDatabase.LoadAssetAtPath<Font>(spec.source);
    if (font == null) return Abort($"source Font not found: {spec.source}");
    fonts.Add(font);
}
var japaneseMedium = AssetDatabase.LoadAssetAtPath<FontAsset>($"{Folder}/GameUiJapanese SDF.asset");
if (japaneseMedium == null) return Abort($"fallback FontAsset not found: {Folder}/GameUiJapanese SDF.asset (run create-game-ui-fonts.dynamic.cs first)");
var koreanMedium = AssetDatabase.LoadAssetAtPath<FontAsset>($"{Folder}/GameUiKorean SDF.asset");
if (koreanMedium == null) return Abort($"fallback FontAsset not found: {Folder}/GameUiKorean SDF.asset (run create-game-ui-fonts.dynamic.cs first)");

// 4 つを一時パスへ作る。1 つでも作れなければ作った一時ファイルを消して止める
// Create all four at temporary paths; if any fails, delete the temporaries and stop
var created = new List<FontAsset>();
for (var i = 0; i < specs.Length; i++)
{
    var asset = CreateAtTemporaryPath(fonts[i], specs[i].name);
    if (asset == null)
    {
        foreach (var spec in specs) AssetDatabase.DeleteAsset(TemporaryPath(spec.name));
        return Abort($"FontAsset.CreateFontAsset returned null for {specs[i].source}");
    }
    created.Add(asset);
}
var koreanBold = created[0];
var japaneseBold = created[1];
var latinBold = created[2];
var latinMedium = created[3];

// 太字は太字どうしでたどる。Medium の Latin は段階 1 の JP/KR（Medium）へ落とす
// Bold falls back within bold faces; the Medium Latin falls back to the stage-1 JP/KR (Medium)
japaneseBold.fallbackFontAssetTable = new List<FontAsset> { koreanBold };
latinBold.fallbackFontAssetTable = new List<FontAsset> { japaneseBold, koreanBold };
latinMedium.fallbackFontAssetTable = new List<FontAsset> { japaneseMedium, koreanMedium };
foreach (var asset in created) { asset.ClearFontAssetData(true); EditorUtility.SetDirty(asset); }
AssetDatabase.SaveAssets();

// 全部作れたので古い asset を消し、一時パスから本来の名前へ移す
// All four exist, so delete the old assets and move the temporaries to their real names
foreach (var spec in specs)
{
    var finalPath = FinalPath(spec.name);
    AssetDatabase.DeleteAsset(finalPath);
    var moveError = AssetDatabase.MoveAsset(TemporaryPath(spec.name), finalPath);
    if (!string.IsNullOrEmpty(moveError)) return Abort($"MoveAsset {TemporaryPath(spec.name)} -> {finalPath} failed: {moveError}");
}

// 作り直すと GUID が変わるので、フォントを指す Typography.uss を読み直す
// Recreation changes the GUIDs, so reimport Typography.uss which points at the fonts
AssetDatabase.ImportAsset("Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss", ImportAssetOptions.ForceUpdate);
return $"latinBold={AssetDatabase.GetAssetPath(latinBold)} japaneseBold={AssetDatabase.GetAssetPath(japaneseBold)} koreanBold={AssetDatabase.GetAssetPath(koreanBold)} latinMedium={AssetDatabase.GetAssetPath(latinMedium)}";

string FinalPath(string assetName) => $"{Folder}/{assetName}.asset";
string TemporaryPath(string assetName) => $"{Folder}/{assetName}.creating.asset";

string Abort(string reason)
{
    Debug.LogError($"[create-game-ui-weight-fonts] aborted: {reason}");
    return $"aborted: {reason}";
}

FontAsset CreateAtTemporaryPath(Font font, string assetName)
{
    var asset = FontAsset.CreateFontAsset(font, 90, 18, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
    if (asset == null) return null;
    asset.name = assetName;
    var path = TemporaryPath(assetName);
    AssetDatabase.DeleteAsset(path);
    AssetDatabase.CreateAsset(asset, path);
    // clearDynamicDataOnBuild は internal のため、シリアライズ済みの欄を立てる
    // clearDynamicDataOnBuild is internal, so set its serialized field instead
    var serialized = new SerializedObject(asset);
    serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    foreach (var texture in asset.atlasTextures) { texture.name = assetName + " Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
    asset.material.name = assetName + " Material";
    AssetDatabase.AddObjectToAsset(asset.material, asset);
    return asset;
}
