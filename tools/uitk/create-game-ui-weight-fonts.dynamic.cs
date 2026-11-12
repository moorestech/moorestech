// GameUi の見出し用の Bold の SDF FontAsset 3 つ（Mantine Title の題の字形。設計書 A-220）を作る。uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)" で実行する
// 元の Font と fallback の FontAsset が全部読めてから作り、3 つとも作れてから古い asset を置き換える（途中で失敗しても既存の FontAsset を消さない）
// Creates the three Bold GameUi SDF FontAssets for headings (the Mantine Title face, design A-220); run with uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)"
// Creation starts only after every source Font and fallback FontAsset loads, and old assets are replaced only after all three are created (a failure never deletes the existing FontAssets)
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
const string Folder = "Assets/UI Toolkit/GameUi/Fonts";
// SDF の atlas の padding（設計書 A-198）。数字の縁を Web に合わせる値で、もう一方のスクリプトと同じにする
// SDF atlas padding (design A-198): the value that matches digit edges to the Web; keep it equal to the sibling script
const int AtlasPadding = 18;
const int SamplingPointSize = 90;
const string FontFolder = "Assets/Dependencies/Font";
var specs = new[]
{
    (source: $"{FontFolder}/NotoSansJP-Bold.ttf", name: "GameUiJapaneseBold SDF"),
    (source: $"{FontFolder}/NotoSans-Bold.ttf", name: "GameUiLatinBold SDF"),
    (source: $"{FontFolder}/NotoSansKR-Bold.ttf", name: "GameUiKoreanBold SDF"),
};

// 元の Font と、fallback にする段階 1 の JP/KR（Medium）を先に全部読む。欠けていれば何も消さずに止める
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

// 3 つを一時パスへ作る。1 つでも作れなければ作った一時ファイルを消して止める
// Create all three at temporary paths; if any fails, delete the temporaries and stop
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
var japaneseBold = created[0];
var latinBold = created[1];
var koreanBold = created[2];

// 和文の題の字形は韓国語の字を KR Bold で描く（Mac の Chrome の Apple SD Gothic Neo Bold と太さが揃う。設計書 A-220）。欧文の題は段階 1 の JP/KR（Medium）へ落とす
// The CJK heading face draws Hangul with KR Bold (matching Apple SD Gothic Neo Bold in Mac Chrome, design A-220); the Latin heading falls back to the stage-1 JP/KR (Medium)
japaneseBold.fallbackFontAssetTable = new List<FontAsset> { koreanBold };
koreanBold.fallbackFontAssetTable = new List<FontAsset>();
latinBold.fallbackFontAssetTable = new List<FontAsset> { japaneseMedium, koreanMedium };
// 和文の題は Bold の 0.925 倍が W6 より太いので SDF の面を細らせる（LX-181 の実測値）。韓国語の字も同じ比で揃える
// The CJK heading at Bold 0.925x is heavier than W6, so thin the SDF face (LX-181 measured value); Hangul matches by the same ratio
japaneseBold.regularStyleWeight = -0.45f;
koreanBold.regularStyleWeight = -0.45f;
foreach (var asset in created) { asset.ClearFontAssetData(true); EditorUtility.SetDirty(asset); }
AssetDatabase.SaveAssets();

// 全部作れたので古い asset を消し、一時パスから本来の名前へ移す
// All exist, so delete the old assets and move the temporaries to their real names
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
return $"latinBold={AssetDatabase.GetAssetPath(latinBold)} japaneseBold={AssetDatabase.GetAssetPath(japaneseBold)} koreanBold={AssetDatabase.GetAssetPath(koreanBold)}";

string FinalPath(string assetName) => $"{Folder}/{assetName}.asset";
string TemporaryPath(string assetName) => $"{Folder}/{assetName}.creating.asset";

string Abort(string reason)
{
    Debug.LogError($"[create-game-ui-weight-fonts] aborted: {reason}");
    return $"aborted: {reason}";
}

FontAsset CreateAtTemporaryPath(Font font, string assetName)
{
    var asset = FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
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
