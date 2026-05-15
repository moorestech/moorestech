using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// 現在のGitブランチ名をツールバーに表示する
    /// Display the current Git branch name in the toolbar
    /// </summary>
    public static class BranchNameToolbarElement
    {
        private const string ElementPath = "moorestech/Branch Name";
        private const string ColorMarker = "moorestech-branch-color-marker";
        private const double PollIntervalSec = 2.0;

        private static string _currentBranchName = "(loading)";
        private static double _nextPollTime;

        // ダークモード・ライトモード両方で視認しやすい色パレット（中明度・適度な彩度）
        // Color palette readable on both dark and light themes (mid lightness, modest saturation)
        private static readonly Color[] Palette =
        {
            new Color32(0xD0, 0x55, 0x55, 0xFF), // red
            new Color32(0xD0, 0x88, 0x44, 0xFF), // orange
            new Color32(0xBE, 0xA0, 0x3E, 0xFF), // amber
            new Color32(0x88, 0xB0, 0x40, 0xFF), // lime
            new Color32(0x55, 0xA8, 0x55, 0xFF), // green
            new Color32(0x45, 0xB0, 0x88, 0xFF), // mint
            new Color32(0x3E, 0xA8, 0xA8, 0xFF), // teal
            new Color32(0x40, 0x98, 0xC8, 0xFF), // sky blue
            new Color32(0x55, 0x80, 0xD0, 0xFF), // blue
            new Color32(0x80, 0x70, 0xD0, 0xFF), // indigo
            new Color32(0xA8, 0x60, 0xC8, 0xFF), // purple
            new Color32(0xC8, 0x55, 0xA8, 0xFF), // magenta
            new Color32(0xC8, 0x55, 0x80, 0xFF), // pink
            new Color32(0xA0, 0x80, 0x60, 0xFF), // brown
            new Color32(0x70, 0x90, 0x70, 0xFF), // sage
            new Color32(0x80, 0x80, 0xA8, 0xFF), // slate
        };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 初回取得とポーリング登録
            // Initial read and polling registration
            _currentBranchName = ReadCurrentBranchName();
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;

            // 新規登録要素は既定で非表示状態のため、内部APIを直接呼んで強制表示する
            // Newly registered elements default to hidden; force-show via internal API
            EditorApplication.delayCall += ForceShow;
        }

        private const string ReloadElementPath = "moorestech/Scene Reload";

        private static void ForceShow()
        {
            #region Internal

            // 内部APIで強制表示し、Reloadボタンと同じMiddleセクションの先頭にドッキングする
            // Force-show via internal API and dock to the start of the Middle section that contains Reload
            CallShowAll();
            DockBeforeReload();

            void CallShowAll()
            {
                var method = typeof(MainToolbar).GetMethod("ShowAll", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                method?.Invoke(null, new object[] { ElementPath });
            }

            void DockBeforeReload()
            {
                var tryGetOverlay = typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic);
                if (tryGetOverlay == null) return;

                var branchArgs = new object[] { ElementPath, null };
                var reloadArgs = new object[] { ReloadElementPath, null };
                tryGetOverlay.Invoke(null, branchArgs);
                tryGetOverlay.Invoke(null, reloadArgs);

                var branchOverlay = branchArgs[1];
                var reloadOverlay = reloadArgs[1];
                if (branchOverlay == null || reloadOverlay == null) return;

                var assembly = typeof(MainToolbar).Assembly;
                var sectionType = assembly.GetType("UnityEditor.Overlays.OverlayContainerSection");
                var hintType = assembly.GetType("UnityEditor.Overlays.DockingHint");
                var containerType = assembly.GetType("UnityEditor.Overlays.OverlayContainer");
                if (sectionType == null || hintType == null || containerType == null) return;

                var overlayBaseType = assembly.GetType("UnityEditor.Overlays.Overlay");
                var containerProp = overlayBaseType.GetProperty("container", BindingFlags.NonPublic | BindingFlags.Instance);
                var reloadContainer = containerProp.GetValue(reloadOverlay);

                // Reloadと同じMiddleセクションのindex 0にドッキング
                // Dock at the start of the same Middle section that holds Reload
                MethodInfo dockAt = null;
                foreach (var m in overlayBaseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name == "DockAt" && m.GetParameters().Length == 4)
                    {
                        dockAt = m;
                        break;
                    }
                }
                if (dockAt == null) return;

                var middleSection = System.Enum.Parse(sectionType, "Middle");
                var dockedBeforeHint = System.Enum.Parse(hintType, "DockedBefore");
                dockAt.Invoke(branchOverlay, new[] { reloadContainer, middleSection, (object)0, dockedBeforeHint });
            }

            #endregion
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = int.MaxValue)]
        public static MainToolbarElement CreateElement()
        {
            // ブランチ名をテキストとして表示するボタン（クリック動作なし）
            // Button that displays the branch name as text (no click handler)
            var content = new MainToolbarContent(_currentBranchName, (Texture2D)null, "現在のGitブランチ名 / Current Git branch name");
            return new MainToolbarButton(content, () => { });
        }

        private static void OnUpdate()
        {
            // ポーリング間隔を絞る
            // Throttle polling to a fixed interval
            if (EditorApplication.timeSinceStartup < _nextPollTime) return;
            _nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSec;

            var newBranch = ReadCurrentBranchName();
            if (newBranch != _currentBranchName)
            {
                _currentBranchName = newBranch;
                MainToolbar.Refresh(ElementPath);
            }

            ColorInjector.TryColorize();
        }

        #region Internal: Git branch reading

        private static string ReadCurrentBranchName()
        {
            // .git の HEAD を解決（ワークツリーの gitdir: 形式にも対応）
            // Resolve HEAD of .git (also supports the gitdir: form used by worktrees)
            var headPath = ResolveHeadPath();
            if (headPath == null || !File.Exists(headPath)) return "(no git)";

            var headContent = File.ReadAllText(headPath).Trim();
            const string refPrefix = "ref: refs/heads/";
            if (headContent.StartsWith(refPrefix))
            {
                return headContent.Substring(refPrefix.Length);
            }

            // detached HEAD: 短縮SHA表示
            // detached HEAD: show short SHA
            if (headContent.Length >= 7)
            {
                return $"(detached: {headContent.Substring(0, 7)})";
            }
            return "(detached)";
        }

        private static string ResolveHeadPath()
        {
            // moorestech_client/Assets から2階層上 = リポジトリルート
            // Two levels up from moorestech_client/Assets = repository root
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var gitPath = Path.Combine(repoRoot, ".git");

            if (Directory.Exists(gitPath))
            {
                return Path.Combine(gitPath, "HEAD");
            }

            if (File.Exists(gitPath))
            {
                // worktree の場合、.git ファイルが gitdir を指す
                // For worktrees, the .git file points to the real gitdir
                var content = File.ReadAllText(gitPath).Trim();
                const string prefix = "gitdir:";
                if (content.StartsWith(prefix))
                {
                    var dir = content.Substring(prefix.Length).Trim();
                    if (!Path.IsPathRooted(dir))
                    {
                        dir = Path.GetFullPath(Path.Combine(repoRoot, dir));
                    }
                    return Path.Combine(dir, "HEAD");
                }
            }

            return null;
        }

        #endregion

        #region Internal: Hash-based color selection

        private static Color GetBranchColor(string branchName)
        {
            if (string.IsNullOrEmpty(branchName)) return Color.gray;

            // ブランチ名のMD5先頭4バイトをパレットindexに変換
            // Map first 4 MD5 bytes of branch name to palette index
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(branchName));
            var idx = (uint)((hashBytes[0] << 24) | (hashBytes[1] << 16) | (hashBytes[2] << 8) | hashBytes[3]);
            return Palette[idx % (uint)Palette.Length];
        }

        #endregion

        // 描画後のボタンテキストにハッシュ由来の色を反映する
        // Apply the hash-derived color to the rendered button text
        [InitializeOnLoad]
        private static class ColorInjector
        {
            static ColorInjector()
            {
                EditorApplication.update -= TryColorize;
                EditorApplication.update += TryColorize;
            }

            public static void TryColorize()
            {
                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (var w in windows)
                {
                    if (w.GetType().FullName != "UnityEditor.MainToolbarWindow") continue;
                    ColorizeRecursive(w.rootVisualElement);
                }
            }

            private static void ColorizeRecursive(VisualElement element)
            {
                // ボタン直下に置かれたテキスト要素を判定して着色する
                // Detect text elements inside the toolbar button and tint them
                if (element is TextElement textElement && textElement.text == _currentBranchName)
                {
                    var color = GetBranchColor(_currentBranchName);
                    textElement.style.color = color;
                    textElement.style.unityFontStyleAndWeight = FontStyle.Bold;

                    // 再ロード後の二重着色を防ぐマーカー
                    // Marker to skip re-tinting after reloads
                    textElement.AddToClassList(ColorMarker);
                }

                foreach (var child in element.Children())
                {
                    ColorizeRecursive(child);
                }
            }
        }
    }
}
