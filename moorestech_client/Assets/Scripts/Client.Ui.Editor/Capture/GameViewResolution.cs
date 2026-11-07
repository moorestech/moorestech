using System;
using System.Reflection;
using UnityEditor;

namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// Game View を固定解像度 W×H にする
    /// Sets the Game View to a fixed W x H resolution (adding it if missing)
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

            // 反射で引く口はすべて AddFixedSize（永続設定の追加）より前に解決する
            // Resolve every reflected member before AddFixedSize, which persists a user setting
            var sizes = RequireProperty(typeof(ScriptableSingleton<>).MakeGenericType(sizesType), "instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).GetValue(null);
            var groupKind = (int)RequireProperty(sizesType, "currentGroupType", AnyInstance).GetValue(sizes);
            var getGroup = RequireMethod(sizesType, "GetGroup", AnyInstance);
            var group = getGroup.Invoke(sizes, new object[] { groupKind });
            var groupType = group.GetType();
            var getTotalCount = RequireMethod(groupType, "GetTotalCount", AnyInstance);
            var getGameViewSize = RequireMethod(groupType, "GetGameViewSize", AnyInstance);
            var addCustomSize = RequireMethod(groupType, "AddCustomSize", AnyInstance);
            var widthProperty = RequireProperty(sizeType, "width", AnyInstance);
            var heightProperty = RequireProperty(sizeType, "height", AnyInstance);
            var sizeTypeProperty = RequireProperty(sizeType, "sizeType", AnyInstance);
            var sizeConstructor = RequireConstructor(sizeType, new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
            var selectSize = RequireMethod(gameViewType, "SizeSelectionCallback", AnyInstance);
            var targetRenderSize = RequireProperty(gameViewType, "targetRenderSize", AnyInstance);
            var index = FindFixedSize() ?? AddFixedSize();

            // Game View を開いて大きさを選ぶ
            // Open the Game View and select the size
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            selectSize.Invoke(gameView, new object[] { index, null });
            var renderSize = targetRenderSize.GetValue(gameView);
            return $"index={index} target={renderSize}";

            #region Internal

            Type RequireType(string typeName)
            {
                var type = editorAssembly.GetType(typeName);
                if (type == null) throw new InvalidOperationException($"[GameViewResolution] {typeName} not found");
                return type;
            }

            MethodInfo RequireMethod(Type owner, string name, BindingFlags flags)
            {
                var method = owner.GetMethod(name, flags);
                if (method == null) throw new InvalidOperationException($"[GameViewResolution] {owner.Name}.{name} not found");
                return method;
            }

            PropertyInfo RequireProperty(Type owner, string name, BindingFlags flags)
            {
                var property = owner.GetProperty(name, flags);
                if (property == null) throw new InvalidOperationException($"[GameViewResolution] {owner.Name}.{name} not found");
                return property;
            }

            ConstructorInfo RequireConstructor(Type owner, Type[] parameterTypes)
            {
                var constructor = owner.GetConstructor(parameterTypes);
                if (constructor == null) throw new InvalidOperationException($"[GameViewResolution] {owner.Name} constructor not found");
                return constructor;
            }

            int? FindFixedSize()
            {
                var total = (int)getTotalCount.Invoke(group, null);
                for (var i = 0; i < total; i++)
                {
                    var size = getGameViewSize.Invoke(group, new object[] { i });
                    var sizeWidth = (int)widthProperty.GetValue(size);
                    var sizeHeight = (int)heightProperty.GetValue(size);
                    var kind = sizeTypeProperty.GetValue(size).ToString();
                    if (sizeWidth == width && sizeHeight == height && kind == FixedResolutionTypeName) return i;
                }
                return null;
            }

            int AddFixedSize()
            {
                var newSize = sizeConstructor.Invoke(new object[] { Enum.Parse(sizeKindType, FixedResolutionTypeName), width, height, $"{width}x{height}" });
                addCustomSize.Invoke(group, new[] { newSize });
                return (int)getTotalCount.Invoke(group, null) - 1;
            }

            #endregion
        }
    }
}
