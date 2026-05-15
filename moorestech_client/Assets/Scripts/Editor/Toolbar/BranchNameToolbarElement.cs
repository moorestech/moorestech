using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Overlays;
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
        private const double PollIntervalSec = 2.0;

        // ダーク・ライト両モードで視認可能になるよう中明度・適度な彩度に揃える
        // Mid lightness and modest saturation so the text stays readable on both themes
        private static readonly Color[] Palette =
        {
            new Color32(0xD0, 0x55, 0x55, 0xFF),
            new Color32(0xD0, 0x88, 0x44, 0xFF),
            new Color32(0xBE, 0xA0, 0x3E, 0xFF),
            new Color32(0x88, 0xB0, 0x40, 0xFF),
            new Color32(0x55, 0xA8, 0x55, 0xFF),
            new Color32(0x45, 0xB0, 0x88, 0xFF),
            new Color32(0x3E, 0xA8, 0xA8, 0xFF),
            new Color32(0x40, 0x98, 0xC8, 0xFF),
            new Color32(0x55, 0x80, 0xD0, 0xFF),
            new Color32(0x80, 0x70, 0xD0, 0xFF),
            new Color32(0xA8, 0x60, 0xC8, 0xFF),
            new Color32(0xC8, 0x55, 0xA8, 0xFF),
            new Color32(0xC8, 0x55, 0x80, 0xFF),
            new Color32(0xA0, 0x80, 0x60, 0xFF),
            new Color32(0x70, 0x90, 0x70, 0xFF),
            new Color32(0x80, 0x80, 0xA8, 0xFF),
        };

        private static string _currentBranchName = "(loading)";
        private static Color _cachedColor = Color.gray;
        private static double _nextPollTime;
        private static bool _colorized;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            _currentBranchName = ReadCurrentBranchName();
            _cachedColor = ComputeBranchColor(_currentBranchName);
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateElement()
        {
            var content = new MainToolbarContent(_currentBranchName, (Texture2D)null, "現在のGitブランチ名 / Current Git branch name");
            return new MainToolbarButton(content, () => { });
        }

        private static void OnUpdate()
        {
            // 描画後にハッシュ由来の色を適用する。成功すれば次回以降スキップ
            // Apply the hash-derived color after rendering; skip once successfully applied
            if (!_colorized) TryColorize();

            if (EditorApplication.timeSinceStartup < _nextPollTime) return;
            _nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSec;

            var newBranch = ReadCurrentBranchName();
            if (newBranch == _currentBranchName) return;

            _currentBranchName = newBranch;
            _cachedColor = ComputeBranchColor(newBranch);
            _colorized = false;
            MainToolbar.Refresh(ElementPath);
        }

        private static string ReadCurrentBranchName()
        {
            var headPath = ResolveHeadPath();
            if (headPath == null || !File.Exists(headPath)) return "(no git)";

            var headContent = File.ReadAllText(headPath).Trim();
            const string refPrefix = "ref: refs/heads/";
            if (headContent.StartsWith(refPrefix))
            {
                return headContent.Substring(refPrefix.Length);
            }

            // detached HEAD は短縮SHAを返す
            // For detached HEAD return the short SHA
            if (7 <= headContent.Length)
            {
                return $"(detached: {headContent.Substring(0, 7)})";
            }
            return "(detached)";
        }

        private static string ResolveHeadPath()
        {
            // moorestech_client/Assets から2階層上がリポジトリルート
            // Two levels up from moorestech_client/Assets is the repository root
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var gitPath = Path.Combine(repoRoot, ".git");

            if (Directory.Exists(gitPath)) return Path.Combine(gitPath, "HEAD");
            if (!File.Exists(gitPath)) return null;

            // worktreeでは .git ファイルが gitdir を指す
            // For worktrees the .git file points to the real gitdir
            var content = File.ReadAllText(gitPath).Trim();
            const string prefix = "gitdir:";
            if (!content.StartsWith(prefix)) return null;

            var dir = content.Substring(prefix.Length).Trim();
            if (!Path.IsPathRooted(dir))
            {
                dir = Path.GetFullPath(Path.Combine(repoRoot, dir));
            }
            return Path.Combine(dir, "HEAD");
        }

        private static Color ComputeBranchColor(string branchName)
        {
            if (string.IsNullOrEmpty(branchName)) return Color.gray;

            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(branchName));
            var idx = (uint)((hashBytes[0] << 24) | (hashBytes[1] << 16) | (hashBytes[2] << 8) | hashBytes[3]);
            return Palette[idx % (uint)Palette.Length];
        }

        private static void TryColorize()
        {
            // ブランチOverlayのrootVisualElementに限定して同名TextElementの誤着色を避ける
            // Scope colorization to the branch Overlay's rootVisualElement to avoid mis-tinting same-text elements elsewhere
            var tryGetOverlay = typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic);
            if (tryGetOverlay == null) return;

            var args = new object[] { ElementPath, null };
            tryGetOverlay.Invoke(null, args);
            if (args[1] is not Overlay overlay) return;

            var rootProp = typeof(Overlay).GetProperty("rootVisualElement", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            var root = rootProp?.GetValue(overlay) as VisualElement;
            if (root == null) return;

            ColorizeRecursive(root);
        }

        private static void ColorizeRecursive(VisualElement element)
        {
            if (element is TextElement textElement && textElement.text == _currentBranchName)
            {
                textElement.style.color = _cachedColor;
                textElement.style.unityFontStyleAndWeight = FontStyle.Bold;
                _colorized = true;
                return;
            }

            foreach (var child in element.Children())
            {
                ColorizeRecursive(child);
                if (_colorized) return;
            }
        }
    }
}
