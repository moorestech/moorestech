using System;
using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class CustomColorPicker : AltoEditorWindow
    {
        public static void ShowWindow(Rect buttonRect, Action<Color> onSelected)
        {
            var window = CreateInstance<CustomColorPicker>();
            window.titleContent = new GUIContent("Color Picker");
            window.onSelected = onSelected;
            window.ShowAsDropDown(buttonRect, new Vector2(150f, 170f));
        }

        public Action<Color> onSelected;

        void OnGUI()
        {
            Header("Color Swatch", LightGray);

            using (new GUILayout.HorizontalScope())
            {
                ColoredButton(new Color(0.16f, 0.16f, 0.16f), 60f);
            }

            using (new GUILayout.HorizontalScope())
            {
                for (int i = 1; i <= 5; ++i) { ColoredButton(GetColor(i)); }
            }
            using (new GUILayout.HorizontalScope())
            {
                for (int i = 6; i <= 10; ++i) { ColoredButton(GetColor(i)); }
            }
            using (new GUILayout.HorizontalScope())
            {
                for (int i = 11; i <= 15; ++i) { ColoredButton(GetColor(i)); }
            }
            using (new GUILayout.HorizontalScope())
            {
                for (int i = 16; i <= 20; ++i) { ColoredButton(GetColor(i)); }
            }

            using (new GUILayout.HorizontalScope())
            {
                for (int i = 1; i <= 5; ++i)
                {
                    float b = 0.2f * i;
                    ColoredButton(new Color(b, b, b));
                }
            }
        }

        Color GetColor(int index)
        {
            float hue = GetHue(index);
            Color color = Color.HSVToRGB(hue, 1.0f, 1.0f);

            float luminance = (color.r * 0.299f) + (color.g * 0.587f) + (color.b * 0.114f);
            return Color.HSVToRGB(hue, 0.6f + luminance * 0.4f, 1.0f - luminance * 0.13f);
        }

        float GetHue(int index)
        {
          float hue = (0.67f - index * 0.05f) % 1.0f;
          return hue - Mathf.Floor(hue);
        }

        void ColoredButton(Color color, float width = 20f)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(
                new GUIContent(""), GUI.skin.button, GUILayout.Width(width)
            );
            if (GUI.Button(buttonRect, "", EditorStyles.boldLabel))
            {
                onSelected?.Invoke(color);
                Close();
            }
            EditorGUI.DrawRect(buttonRect, color);
        }
    }
}
