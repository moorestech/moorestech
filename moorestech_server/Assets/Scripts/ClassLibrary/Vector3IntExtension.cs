using UnityEngine;

namespace ClassLibrary
{
    public static class Vector3IntExtension
    {
        public static Vector3Int Abs(this Vector3Int vector3Int)
        {
            return new Vector3Int(Mathf.Abs(vector3Int.x), Mathf.Abs(vector3Int.y), Mathf.Abs(vector3Int.z));
        }
    }
}