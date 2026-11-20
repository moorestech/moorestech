// 起動中の Editor の GPU スキニングの設定を切り替える（ProjectSettings への保存はしない）。引数の代わりに AppDomain の値を見る
using UnityEditor;
var before = PlayerSettings.gpuSkinning;
PlayerSettings.gpuSkinning = false;
return "gpuSkinning " + before + " -> " + PlayerSettings.gpuSkinning;
