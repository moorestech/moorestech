// GameUi の太さ違いの SDF FontAsset 4 つを作る。uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)" で実行する
// Creates the four weight-variant GameUi SDF FontAssets; run with uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-weight-fonts.dynamic.cs)"
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
const string Folder = "Assets/UI Toolkit/GameUi/Fonts";
FontAsset Create(string sourcePath, string assetName)
{
    var path = $"{Folder}/{assetName}.asset";
    AssetDatabase.DeleteAsset(path);
    var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
    var asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
    asset.name = assetName;
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
FontAsset Load(string assetName) => AssetDatabase.LoadAssetAtPath<FontAsset>($"{Folder}/{assetName}.asset");
var koreanBold = Create("Assets/Dependencies/Font/NotoSansKR-Bold.ttf", "GameUiKoreanBold SDF");
var japaneseBold = Create("Assets/Dependencies/Font/NotoSansJP-Bold.ttf", "GameUiJapaneseBold SDF");
var latinBold = Create("Assets/Dependencies/Font/NotoSans-Bold.ttf", "GameUiLatinBold SDF");
var latinMedium = Create("Assets/Dependencies/Font/NotoSans-Medium.ttf", "GameUiLatinMedium SDF");
// 太字は太字どうしでたどる。Medium の Latin は段階 1 の JP/KR（Medium）へ落とす
// Bold falls back within bold faces; the Medium Latin falls back to the stage-1 JP/KR (Medium)
japaneseBold.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { koreanBold };
latinBold.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { japaneseBold, koreanBold };
latinMedium.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { Load("GameUiJapanese SDF"), Load("GameUiKorean SDF") };
foreach (var asset in new[] { koreanBold, japaneseBold, latinBold, latinMedium }) { asset.ClearFontAssetData(true); EditorUtility.SetDirty(asset); }
AssetDatabase.SaveAssets();
// 作り直すと GUID が変わるので、フォントを指す Typography.uss を読み直す
// Recreation changes the GUIDs, so reimport Typography.uss which points at the fonts
AssetDatabase.ImportAsset("Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss", ImportAssetOptions.ForceUpdate);
return $"latinBold={AssetDatabase.GetAssetPath(latinBold)} japaneseBold={AssetDatabase.GetAssetPath(japaneseBold)} koreanBold={AssetDatabase.GetAssetPath(koreanBold)} latinMedium={AssetDatabase.GetAssetPath(latinMedium)}";
