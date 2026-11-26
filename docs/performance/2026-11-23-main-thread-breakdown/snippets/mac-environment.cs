// 計測条件を JSON で返す（Mac 用。benchmark/snippets/environment.cs の Windows の窓の状態を除いたもの）
using UnityEngine;
var cam = Camera.main;
return "{\"unityVersion\":\"" + Application.unityVersion + "\",\"codeOptimization\":\"" + UnityEditor.Compilation.CompilationPipeline.codeOptimization + "\",\"quality\":\"" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\",\"vSyncCount\":" + QualitySettings.vSyncCount + ",\"targetFrameRate\":" + Application.targetFrameRate + ",\"cameraPixels\":\"" + (cam ? cam.pixelWidth + "x" + cam.pixelHeight : "none") + "\",\"gfx\":\"" + SystemInfo.graphicsDeviceType + "\",\"gpu\":\"" + SystemInfo.graphicsDeviceName + "\",\"cpu\":\"" + SystemInfo.processorType + "\",\"batchMode\":" + (Application.isBatchMode ? "true" : "false") + "}";
