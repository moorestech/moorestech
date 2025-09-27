#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class ScreenCapture
    {
        [MenuItem(AltoMenuPath.ScreenCapture + "x 1")]
        static void CaptureScreen_x1()
        {
            CaptureScreen(1);
        }

        [MenuItem(AltoMenuPath.ScreenCapture + "x 2")]
        static void CaptureScreen_x2()
        {
            CaptureScreen(2);
        }

        [MenuItem(AltoMenuPath.ScreenCapture + "x 3")]
        static void CaptureScreen_x3()
        {
            CaptureScreen(3);
        }

        [MenuItem(AltoMenuPath.ScreenCapture + "x 4")]
        static void CaptureScreen_x4()
        {
            CaptureScreen(4);
        }

        static void CaptureScreen(int superSize)
        {
            if (Camera.main == null || !Camera.main.enabled)
            {
                Debug.LogError("カメラが enabled = false の場合にキャプチャを撮ると Unity がクラッシュするようなので処理をキャンセルしました");
                return;
            }

            DateTime now = DateTime.Now;
            string timestamp = $"{ now.Year }-{ now.Month.ToString("D2") }{ now.Day.ToString("D2") }-"
                             + $"{ now.Hour.ToString("D2") }{ now.Minute.ToString("D2") }{ now.Second.ToString("D2") }";
            string filePath = $"~/Desktop/{ Application.productName }-{ timestamp }.png";
            UnityEngine.ScreenCapture.CaptureScreenshot(filePath, superSize);
        }
    }
}
#endif
