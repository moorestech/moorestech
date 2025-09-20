#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class ColoredProjectViewConfig : AltoEditorWindow
    {
        const string MenuPath = AltoMenuPath.EditorExt + "ColoredProjectView Config";

        [MenuItem(MenuPath)]
        static void ShowWindow()
        {
            GetWindow<ColoredProjectViewConfig>("Colored ProjectView Config");
        }

        //----------------------------------------------------------------------
        // Data structure
        //----------------------------------------------------------------------

        [System.Serializable]
        public class ColorRecord
        {
            public string pattern;
            public string color1;
            public string color2;
            public int depth;

            public Color GetColor1()
            {
                ColorUtility.TryParseHtmlString('#' + color1, out var color);
                return color;
            }

            public Color GetColor2()
            {
                ColorUtility.TryParseHtmlString('#' + color2, out var color);
                return color;
            }

            public void SetColor1(Color color)
            {
                color1 = ColorUtility.ToHtmlStringRGBA(color);
            }

            public void SetColor2(Color color)
            {
                color2 = ColorUtility.ToHtmlStringRGBA(color);
            }
        }

        [System.Serializable]
        public class ColorSettings
        {
            public List<ColorRecord> records = new();
        }

        static ColorSettings _settingsCache = null;
        ColorSettings _settings
        {
            get { return _settingsCache ??= LoadSettings(); }
        }

        ColorSettings LoadSettings()
        {
            return base.LoadSettings<ColorSettings>() ?? NewSettings();
        }

        /// <summary>
        /// Called from ColoredProjectView
        /// </summary>
        internal static ColorSettings GetColorSettings()
        {
            if (_settingsCache != null) { return _settingsCache; }

            var window = ScriptableObject.CreateInstance<ColoredProjectViewConfig>();
            var settings = window.LoadSettings();
            DestroyImmediate(window);
            return settings;
        }

        //----------------------------------------------------------------------
        // Draw GUI
        //----------------------------------------------------------------------

        Vector2 _scrollView;
        string _exportText;
        float _alphaLeft = 0.11f;
        float _alphaRight = 0.31f;

        void OnGUI()
        {
            _scrollView = GUILayout.BeginScrollView(_scrollView);
            {
                Header("Project View Color Settings");
                foreach (var record in _settings.records)
                {
                    bool isCanceled = DrawSettingRow(record);
                    if (isCanceled) { break; }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (Button("[+] Add Item", 100f))
                    {
                        AddNewSettingRecord();
                    }
                    GUILayout.FlexibleSpace();
                    if (Button("Swap All", 80f))
                    {
                        BulkSwapColor();
                    }
                }

                BR();
                using (new GUILayout.HorizontalScope())
                {
                    _alphaLeft = EditorGUILayout.Slider("Left Color Alpha", _alphaLeft, 0f, 1f);
                    if (Button("Set Alpha to\nAll Left Color", 160f))
                    {
                        BulkSetAlpha(_alphaLeft, isLeft: true);
                    }
                }
                using (new GUILayout.HorizontalScope())
                {
                    _alphaRight = EditorGUILayout.Slider("Right Color Alpha", _alphaRight, 0f, 1f);
                    if (Button("Set Alpha to\nAll Right Color", 160f))
                    {
                        BulkSetAlpha(_alphaRight, isLeft: false);
                    }
                }

                BR();
                Header("Save & Load");
                using (new GUILayout.HorizontalScope())
                {
                    if (Button("Apply", 80f, color: LightGreen))
                    {
                        ColoredProjectView.NeedToReloadSettings = true;
                    }
                    if (Button("Reset to Default", 160f))
                    {
                        _settingsCache = NewSettings();
                    }
                    if (Button("Load", 80f, color: LightBlue))
                    {
                        _settingsCache = LoadSettings();
                    }
                    if (Button("Save", 80f, color: LightRed))
                    {
                        SaveSettings(_settingsCache);
                    }
                }

                BR();
                DrawHelp();

                BR();
                BR();
                Header("Export / Import via JSON Text");
                using (new GUILayout.HorizontalScope())
                {
                    if (Button("Export", 100f))
                    {
                        ExportToJson();
                    }
                    if (Button("Import", 100f, color: LightBlue))
                    {
                        ImportFromJson();
                    }
                }
                _exportText = GUILayout.TextArea(_exportText);
            }
            GUILayout.EndScrollView();
        }

        bool DrawSettingRow(ColorRecord record)
        {
            bool isCanceled = false;
            using (new GUILayout.HorizontalScope())
            {
                var content = new GUIContent("▲", "Move Up");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    MoveElement(record, -1);
                    isCanceled = true;
                }
                content = new GUIContent("▼", "Move Down");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    MoveElement(record, +1);
                    isCanceled = true;
                }

                record.pattern = EditorGUILayout.TextField(record.pattern);
                record.depth   = EditorGUILayout.IntField(record.depth, GUILayout.MaxWidth(20f));

                var color1 = record.GetColor1();
                color1 = EditorGUILayout.ColorField(color1);
                record.SetColor1(color1);

                var color2 = record.GetColor2();
                color2 = EditorGUILayout.ColorField(color2);
                record.SetColor2(color2);

                content = new GUIContent("LtoR", "Copy Left RGB to Right");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    Color leftColor = record.GetColor1();
                    leftColor.a = record.GetColor2().a;
                    record.SetColor2(leftColor);
                }

                content = new GUIContent("Swap", "Swap Color");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    (record.color2, record.color1) = (record.color1, record.color2);
                }

                content = new GUIContent("X", "Remove Item");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    _settings.records.Remove(record);
                    isCanceled = true;
                }
            }
            return isCanceled;
        }

        void DrawHelp()
        {
            EditorGUILayout.HelpBox
            (
@"- Match patterns are case-insensitive.
- Numbers indicate the depth. If you specify a value of 1 or higher, only assets at that depth will be colored.
- When the depth is set to 1 or higher, the wildcard * can be used.

・マッチパターンは大文字小文字を区別しません
・数値は深度指定です。1 以上の値を指定した場合、その深度のアセットにのみ着色します
・深度に 1 以上を指定している場合、ワイルドカード「*」が使用可能です",
                MessageType.Info
            );
        }

        //----------------------------------------------------------------------
        // Data Operation
        //----------------------------------------------------------------------

        void BulkSetAlpha(float alpha, bool isLeft)
        {
            foreach (var record in _settings.records)
            {
                if (isLeft)
                {
                    var color = record.GetColor1();
                    color.a = alpha;
                    record.SetColor1(color);
                }
                else
                {
                    var color = record.GetColor2();
                    color.a = alpha;
                    record.SetColor2(color);
                }
            }
        }

        void BulkSwapColor()
        {
            foreach (var record in _settings.records)
            {
                (record.color2, record.color1) = (record.color1, record.color2);
            }
        }

        void MoveElement(ColorRecord record, int moveAmount)
        {
            var records = _settings.records;
            int index = records.IndexOf(record);
            if (index < 0) { return; }
            if (index + moveAmount >= records.Count) { return; }
            if (index + moveAmount < 0) { return; }

            (records[index + moveAmount], records[index]) = (records[index], records[index + moveAmount]);
        }

        //----------------------------------------------------------------------
        // Export / Import via Json Text
        //----------------------------------------------------------------------

        void ExportToJson()
        {
            _exportText = JsonUtility.ToJson(_settings);
        }

        void ImportFromJson()
        {
            var settings = JsonUtility.FromJson<ColorSettings>(_exportText);
            if (settings == null)
            {
                Debug.LogError("Import Format Error");
                return;
            }
            _settingsCache = settings;
        }

        //----------------------------------------------------------------------
        // Default settings
        //----------------------------------------------------------------------

        static ColorSettings NewSettings()
        {
            return new ColorSettings()
            {
                records = new()
                {
                    new()
                    {
                        pattern = "*",
                        color1  = "C3B40040",
                        color2  = "E7A8FD40",
                        depth   = 1,
                    },
                    new()
                    {
                        pattern = "altoframework",
                        color1  = "64C32B0D",
                        color2  = "64C32B4C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "altolib",
                        color1  = "63CFA50D",
                        color2  = "63CFA54C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "altoeditor",
                        color1  = "7D25C80D",
                        color2  = "7D25C84C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "altoshader",
                        color1  = "D43DB60D",
                        color2  = "D43DB64C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "editor",
                        color1  = "B314DD0D",
                        color2  = "B314DD4C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "script",
                        color1  = "3535F80D",
                        color2  = "3535F84C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "shader",
                        color1  = "D4077B0D",
                        color2  = "D4077B4C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "scene",
                        color1  = "E913130D",
                        color2  = "E913134C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "material",
                        color1  = "63C3000D",
                        color2  = "63C3004C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "mesh",
                        color1  = "0B8E1B0D",
                        color2  = "0B8E1B4C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "prefab",
                        color1  = "1CC0DD0D",
                        color2  = "1CC0DD4C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "sprite",
                        color1  = "C5CC750D",
                        color2  = "C5CC754C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "texture",
                        color1  = "36EC900D",
                        color2  = "36EC904C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "animation",
                        color1  = "EC87150D",
                        color2  = "EC87154C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "audio",
                        color1  = "C011630D",
                        color2  = "C011634C",
                        depth   = 0,
                    },
                    new()
                    {
                        pattern = "resource",
                        color1  = "FF61000D",
                        color2  = "FF61004C",
                        depth   = 0,
                    },
                },
            };
        }

        void AddNewSettingRecord()
        {
            _settings.records.Add(new()
            {
                pattern = "",
                color1  = "ffff000d",
                color2  = "ffff004c",
            });
        }
    }
}
#endif
