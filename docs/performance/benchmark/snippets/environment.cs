// 計測条件（最適化モード・品質・解像度・表示状態など）をJSONで返す
using System;
using System.Runtime.InteropServices;
using UnityEngine;
var cam = Camera.main;
var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
// LASTINPUTINFO は {cbSize, dwTime} の8バイト構造体なので uint 配列で受ける
var lii = new uint[] { 8u, 0u };
GetLastInputInfo(lii);
var idleSeconds = (Environment.TickCount - (int)lii[1]) / 1000.0;
return "{\"unityVersion\":\"" + Application.unityVersion + "\",\"codeOptimization\":\"" + UnityEditor.Compilation.CompilationPipeline.codeOptimization + "\",\"quality\":\"" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\",\"vSyncCount\":" + QualitySettings.vSyncCount + ",\"targetFrameRate\":" + Application.targetFrameRate + ",\"cameraPixels\":\"" + (cam ? cam.pixelWidth + "x" + cam.pixelHeight : "none") + "\",\"gpu\":\"" + SystemInfo.graphicsDeviceName + "\",\"cpu\":\"" + SystemInfo.processorType + "\",\"editorWindowMinimized\":" + (IsIconic(handle) ? "true" : "false") + ",\"editorWindowForeground\":" + (GetForegroundWindow() == handle ? "true" : "false") + ",\"userIdleSeconds\":" + idleSeconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "}";


[DllImport("user32.dll")] static extern bool GetLastInputInfo(uint[] info);
[DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
