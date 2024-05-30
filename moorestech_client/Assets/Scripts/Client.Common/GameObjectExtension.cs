using UnityEngine;

namespace Client.Common
{
    public static class GameObjectExtension
    {
        public static string GetFullPath(this GameObject obj)
        {
            return GetFullPath(obj.transform);
        }
        
        public static string GetFullPath(this Transform t)
        {
            var path = t.name;
            var parent = t.parent;
            while (parent)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }
            
            return path;
        }
    }
}