// 計測するコミットの水の材質のシェーダーを返す（変更前は Shader Graph、変更後は Moorestech/FluidPipeTransparent）
var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Asset/Block/Materials/FluidPipe/FluidPipe_Water.mat");
return "water shader=" + m.shader.name + " gfx=" + UnityEngine.SystemInfo.graphicsDeviceType;
