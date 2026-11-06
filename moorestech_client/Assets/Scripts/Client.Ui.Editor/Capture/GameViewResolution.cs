using System;
using System.Reflection;
using UnityEditor;

namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// Game View を固定解像度 W×H にする（無ければ足す）。並べ比べの 1280×720・2560×1080 の切り替えに使う
    /// Sets the Game View to a fixed W x H resolution (adding it if missing); switches between 1280x720 and 2560x1080 for comparisons
    /// </summary>
    public static class GameViewResolution
    {
        private const string FixedResolutionTypeName = "FixedResolution";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 選んだ番号と Game View の描画寸法を返す
        // Returns the selected index and the Game View render size
        public static string Set(int width, int height)
        {
            // Game View の大きさの一覧は internal なので反射で引く。見つからなければ Unity の更新で口が変わったので止める
            // The Game View size list is internal, so reach it via reflection; stop if a Unity update renamed it
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = RequireType("UnityEditor.GameViewSizes");
            var sizeType = RequireType("UnityEditor.GameViewSize");
            var sizeKindType = RequireType("UnityEditor.GameViewSizeType");
            var gameViewType = RequireType("UnityEditor.GameView");

            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupKind = (int)sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { groupKind });
            var index = FindFixedSize() ?? AddFixedSize();

            // Game View の窓を開いて（無ければ作って）その大きさを選ぶ
            // Open the Game View window (creating it if needed) and select the size
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameViewType.GetMethod("SizeSelectionCallback", AnyInstance).Invoke(gameView, new object[] { index, null });
            var renderSize = gameViewType.GetProperty("targetRenderSize", AnyInstance).GetValue(gameView);
            return $"index={index} target={renderSize}";

            #region Internal

            Type RequireType(string typeName)
            {
                var type = editorAssembly.GetType(typeName);
                if (type == null) throw new InvalidOperationException($"[GameViewResolution] {typeName} not found");
                return type;
            }

            int? FindFixedSize()
            {
                var total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                for (var i = 0; i < total; i++)
                {
                    var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var sizeWidth = (int)sizeType.GetProperty("width").GetValue(size);
                    var sizeHeight = (int)sizeType.GetProperty("height").GetValue(size);
                    var kind = sizeType.GetProperty("sizeType").GetValue(size).ToString();
                    if (sizeWidth == width && sizeHeight == height && kind == FixedResolutionTypeName) return i;
                }
                return null;
            }

            int AddFixedSize()
            {
                var ctor = sizeType.GetConstructor(new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
                var newSize = ctor.Invoke(new object[] { Enum.Parse(sizeKindType, FixedResolutionTypeName), width, height, $"{width}x{height}" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                return (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }

            #endregion
        }
    }
}
