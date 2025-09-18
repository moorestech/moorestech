using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace AltoEditor
{
    public class FavoritesWindow : AltoEditorWindow
    {
        [MenuItem(AltoMenuPath.EditorWindow + "Favorites Window")]
        static void ShowWindow()
        {
            GetWindow<FavoritesWindow>("★ Favorites");
        }

        Vector2 _scrollView;
        bool _highlightRecent;

        const string DefaultColor = "292929";

        //----------------------------------------------------------------------
        // Data structure
        //----------------------------------------------------------------------

        [System.Serializable]
        class AssetInfo
        {
            public string guid;
            public string path;
            public string name;
            public string type;
            public string color;
            public long time;  // last opened unixtime [sec]

            [NonSerialized] public int newestRank;
        }

        [System.Serializable]
        class AssetInfoList
        {
            public List<AssetInfo> infoList = new();
        }

        [SerializeField] static AssetInfoList _assetsCache = null;
        AssetInfoList _assets
        {
            get { return _assetsCache ??= LoadPrefs(); }
        }

        AssetInfo GetAssetInfo(string guid)
        {
            return _assets.infoList.FirstOrDefault(_ => _.guid == guid);
        }

        //----------------------------------------------------------------------
        // Save and Load
        //----------------------------------------------------------------------

        void SavePrefs()
        {
            SaveSettings(_assetsCache);
        }

        AssetInfoList LoadPrefs()
        {
            return LoadSettings<AssetInfoList>() ?? new();
        }

        //----------------------------------------------------------------------
        // Draw GUI
        //----------------------------------------------------------------------

        void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                if (Button("★ Fav", 100f, 40f, "Bookmark selecting asset"))
                {
                    BookmarkAsset();
                }
                DrawSortButtons();
            }

            AssignNewestRank(_assets.infoList);

            _scrollView = GUILayout.BeginScrollView(_scrollView);
            {
                bool isCanceled;
                foreach (var info in _assets.infoList)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        isCanceled = DrawAssetRow(info);
                    }
                    if (isCanceled) { break; }
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    _highlightRecent = GUILayout.Toggle(_highlightRecent, "Highlight recent");
                }
            }
            GUILayout.EndScrollView();
        }

        void DrawSortButtons()
        {
            var sortFuncs = new(string, Action)[]
            {
                ("▼ Type", SortByType),
                ("▲ Type", SortByTypeDesc),
                ("▼ Name", SortByName),
                ("▲ Name", SortByNameDesc),
                ("▼ Color", SortByColor),
                ("▲ Color", SortByColorDesc),
                ("▼ Time", SortByLastOpenedAt),
                ("▲ Time", SortByLastOpenedAtDesc),
            };
            for (int i = 0; i < 4; ++i)
            {
                using (new GUILayout.VerticalScope())
                {
                    for (int j = 0; j < 2; ++j)
                    {
                        var funcInfo = sortFuncs[i * 2 + j];
                        string buttonLabel = funcInfo.Item1;
                        Action sortFunc    = funcInfo.Item2;
                        if (Button(buttonLabel, 100f, 20f, color: LightGray))
                        {
                            sortFunc();
                            SavePrefs();
                        }
                    }
                }
            }
            GUILayout.FlexibleSpace();
        }

        bool DrawAssetRow(AssetInfo info)
        {
            bool isCanceled = false;
            {
                var content = new GUIContent("◎", "Highlight asset");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    HighlightAsset(info);
                }
            }
            {
                SelectColorButton(info);
                DrawAssetItemButton(info);
            }
            {
                var content = new GUIContent("X", "Remove from Favs");
                if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
                {
                    RemoveAsset(info);
                    isCanceled = true;
                }
            }
            return isCanceled;
        }

        void SelectColorButton(AssetInfo info)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(
                new GUIContent(""), GUI.skin.button, GUILayout.Width(14f)
            );
            if (GUI.Button(buttonRect, "", EditorStyles.boldLabel))
            {
                Vector2 screenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.y));
                buttonRect = new Rect(screenPos, buttonRect.size);
                CustomColorPicker.ShowWindow(buttonRect, color => OnSelectColor(info, color));
            }

            buttonRect.y += 1f;
            buttonRect.height -= 3f;
            string colorCode = !string.IsNullOrEmpty(info.color) ? info.color : DefaultColor;
            ColorUtility.TryParseHtmlString($"#{colorCode}", out var color);
            EditorGUI.DrawRect(buttonRect, color);
        }

        void OnSelectColor(AssetInfo info, Color color)
        {
            var targetInfo = GetAssetInfo(info.guid);
            targetInfo.color = ColorUtility.ToHtmlStringRGB(color);
            SavePrefs();
        }

        void DrawAssetItemButton(AssetInfo info)
        {
            var content = new GUIContent($" {info.name}", AssetDatabase.GetCachedIcon(info.path));
            var style = GUI.skin.button;
            var originalAlignment = style.alignment;
            var originalFontStyle = style.fontStyle;
            var originalTextColor = style.normal.textColor;
            style.alignment = TextAnchor.MiddleLeft;
            SetRowStyle(info, style);

            float width = position.width - 90f;
            if (GUILayout.Button(content, style, GUILayout.MaxWidth(width), GUILayout.Height(18)))
            {
                OpenAsset(info);
            }

            style.alignment        = originalAlignment;
            style.fontStyle        = originalFontStyle;
            style.normal.textColor = originalTextColor;
        }

        //----------------------------------------------------------------------
        // Highlight newer rows
        //----------------------------------------------------------------------

        void AssignNewestRank(List<AssetInfo> infoList)
        {
            var ordered = infoList.OrderByDescending(_ => _.time);
            int rank = 1;
            foreach (var info in ordered)
            {
                if (info.time == 0) { continue; }
                info.newestRank = rank;
                ++rank;
            }
        }

        void SetRowStyle(AssetInfo info, GUIStyle style)
        {
            if (!_highlightRecent) { return; }

            int rank = info.newestRank;
            if (rank == 1)
            {
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = new Color(1f, 1f, 0f);
            }
            if (2 <= rank && rank <= 5)
            {
                style.fontStyle = FontStyle.Bold;
                float b1 = 0.85f + (5 - rank) * 0.05f;
                float b2 = 0.70f + (5 - rank) * 0.10f;
                style.normal.textColor = new Color(b1, b2 * 0.85f, 0.5f);
            }
        }

        //----------------------------------------------------------------------
        // Control bookmarked assets
        //----------------------------------------------------------------------

        void BookmarkAsset()
        {
            foreach (string assetGuid in Selection.assetGUIDs)
            {
                if (_assets.infoList.Exists(x => x.guid == assetGuid)) { continue; }

                var info = new AssetInfo
                {
                    guid  = assetGuid,
                    path  = AssetDatabase.GUIDToAssetPath(assetGuid),
                    color = DefaultColor,
                    time  = 0,
                };
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(info.path);
                info.name = asset.name;
                info.type = asset.GetType().ToString();
                _assets.infoList.Add(info);
            }
            SavePrefs();
        }

        void RemoveAsset(AssetInfo info)
        {
            _assets.infoList.Remove(info);
            SavePrefs();
        }

        void HighlightAsset(AssetInfo info)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(info.path);
            EditorGUIUtility.PingObject(asset);
        }

        void OpenAsset(AssetInfo info)
        {
            // Record last opened time
            info.time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SavePrefs();

            // Open scene asset
            if (Path.GetExtension(info.path).Equals(".unity"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(info.path, OpenSceneMode.Single);
                }
                return;
            }

            // Open other type asset
            var asset = AssetDatabase.LoadAssetAtPath<Object>(info.path);
            AssetDatabase.OpenAsset(asset);
        }

        //----------------------------------------------------------------------
        // Sort
        //----------------------------------------------------------------------

        void SortByType()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = a.type.CompareTo(b.type);
                if (cmp1 != 0) { return cmp1; }
                return a.name.CompareTo(b.name);
            });
        }

        void SortByTypeDesc()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = b.type.CompareTo(a.type);
                if (cmp1 != 0) { return cmp1; }
                return a.name.CompareTo(b.name);
            });
        }

        void SortByName()
        {
            _assets.infoList.Sort((a, b) => {
                return a.name.CompareTo(b.name);
            });
        }

        void SortByNameDesc()
        {
            _assets.infoList.Sort((a, b) => {
                return b.name.CompareTo(a.name);
            });
        }

        void SortByColor()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = a.color.CompareTo(b.color);
                if (cmp1 != 0) { return cmp1; }
                int cmp2 = a.type.CompareTo(b.type);
                if (cmp2 != 0) { return cmp2; }
                return a.name.CompareTo(b.name);
            });
        }

        void SortByColorDesc()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = b.color.CompareTo(a.color);
                if (cmp1 != 0) { return cmp1; }
                int cmp2 = a.type.CompareTo(b.type);
                if (cmp2 != 0) { return cmp2; }
                return a.name.CompareTo(b.name);
            });
        }

        void SortByLastOpenedAt()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = b.time.CompareTo(a.time);
                if (cmp1 != 0) { return cmp1; }
                int cmp2 = a.type.CompareTo(b.type);
                if (cmp2 != 0) { return cmp2; }
                return a.name.CompareTo(b.name);
            });
        }

        void SortByLastOpenedAtDesc()
        {
            _assets.infoList.Sort((a, b) => {
                int cmp1 = a.time.CompareTo(b.time);
                if (cmp1 != 0) { return cmp1; }
                int cmp2 = a.type.CompareTo(b.type);
                if (cmp2 != 0) { return cmp2; }
                return a.name.CompareTo(b.name);
            });
        }
    }
}
