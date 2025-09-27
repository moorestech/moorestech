#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace AltoEditor
{
    public class ToggleCompileLock
    {
        const string MenuPath = AltoMenuPath.DevCommands + "Lock Auto Compile";

        [MenuItem(MenuPath)]
        static void ToggleLock()
        {
            var isLocked = Menu.GetChecked(MenuPath);
            if (isLocked)
            {
                Debug.Log("自動コンパイルを有効化（Unity デフォルトの挙動）");
                EditorApplication.UnlockReloadAssemblies();
            }
            else
            {
                Debug.Log("自動コンパイルを無効化。Ctrl + R で手動コンパイルできます");
                EditorApplication.LockReloadAssemblies();
            }
            Menu.SetChecked(MenuPath, !Menu.GetChecked(MenuPath));
        }
    }
}
#endif
