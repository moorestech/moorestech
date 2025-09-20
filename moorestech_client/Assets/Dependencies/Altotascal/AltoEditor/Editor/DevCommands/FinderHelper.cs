using UnityEngine;
using UnityEditor;

namespace AltoEditor
{
    public class FinderHelper
    {
        [MenuItem(AltoMenuPath.DevCommands + "Open Data Path in Finder")]
        public static void OpenPersistentDataPath()
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                System.Diagnostics.Process.Start(Application.persistentDataPath);
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
        }
    }
}
