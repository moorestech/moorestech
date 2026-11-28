// 起動中の Editor のメッシュ変形の方式（PlayerSettings の meshDeformation: 0=CPU, 1=GPU, 2=GPU Batched）を、ProjectSettings に保存せずに切り替える
using UnityEditor;
var target = int.Parse(System.IO.File.ReadAllText(@"C:\moorestech-perf\mesh-deformation.txt").Trim());
var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
var p = so.FindProperty("meshDeformation");
var before = p.intValue;
p.intValue = target;
so.ApplyModifiedPropertiesWithoutUndo();
var after = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings")).FindProperty("meshDeformation").intValue;
return "meshDeformation " + before + " -> " + after + " gpuSkinning=" + PlayerSettings.gpuSkinning;
