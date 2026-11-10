// GameUi の SDF FontAsset 3 つを作り直す。uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-fonts.dynamic.cs)" で実行する（S10）
// Recreates the three GameUi SDF FontAssets; run with uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-fonts.dynamic.cs)" (S10)
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
const string Folder = "Assets/UI Toolkit/GameUi/Fonts";
if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/UI Toolkit/GameUi", "Fonts");
FontAsset Create(string sourcePath, string assetName)
{
    var path = $"{Folder}/{assetName}.asset";
    AssetDatabase.DeleteAsset(path);
    var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
    var asset = FontAsset.CreateFontAsset(font, 90, 18, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
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
var korean = Create("Assets/Dependencies/Font/NotoSansKR-Medium.otf", "GameUiKorean SDF");
var japanese = Create("Assets/Dependencies/Font/NotoSansJP-Medium.otf", "GameUiJapanese SDF");
var latin = Create("Assets/Dependencies/Font/NotoSans-Regular.ttf", "GameUiLatin SDF");
japanese.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { korean };
latin.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { japanese, korean };
foreach (var asset in new[] { korean, japanese, latin }) { asset.ClearFontAssetData(true); EditorUtility.SetDirty(asset); }
AssetDatabase.SaveAssets();
// 作り直すと GUID が変わるので、フォントを指す Typography.uss を読み直す
// Recreation changes the GUIDs, so reimport Typography.uss which points at the fonts
AssetDatabase.ImportAsset("Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss", ImportAssetOptions.ForceUpdate);
return $"latin={AssetDatabase.GetAssetPath(latin)} japanese={AssetDatabase.GetAssetPath(japanese)} korean={AssetDatabase.GetAssetPath(korean)}";
