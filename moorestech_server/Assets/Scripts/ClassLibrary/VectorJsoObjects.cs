using Newtonsoft.Json;
using UnityEngine;

namespace ClassLibrary
{
    [System.Serializable]
    public class Vector2IntJsoObjects
    {
        public int x;
        public int y;

        public Vector2IntJsoObjects(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        
        public Vector2IntJsoObjects() { }
        
        public Vector2IntJsoObjects(Vector2Int v)
        {
            x = v.x;
            y = v.y;
        }
        
        [JsonIgnore] public Vector2Int Vector2Int => new Vector2Int(x, y);
    }

    [System.Serializable]
    public class Vector2JsoObjects
    {
        public float x;
        public float y;

        public Vector2JsoObjects(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        
        public Vector2JsoObjects() { }
        
        public Vector2JsoObjects(Vector2 v)
        {
            x = v.x;
            y = v.y;
        }
        
        [JsonIgnore] public Vector2 Vector2 => new Vector2(x, y);
    }

    [System.Serializable]
    public class Vector3JsoObjects
    {
        public float x;
        public float y;
        public float z;

        public Vector3JsoObjects(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public Vector3JsoObjects(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
        
        public Vector3JsoObjects() { }
        
        [JsonIgnore] public Vector3 Vector3 => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class Vector3IntJsoObjects
    {
        public int x;
        public int y;
        public int z;

        public Vector3IntJsoObjects(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public Vector3IntJsoObjects() { }
        
        public Vector3IntJsoObjects(Vector3Int v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
        
        [JsonIgnore] public Vector3Int Vector3Int => new Vector3Int(x, y, z);
    }

    [System.Serializable]
    public class Vector4JsoObjects
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4JsoObjects(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        
        public Vector4JsoObjects() { }
        
        public Vector4JsoObjects(Vector4 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
            w = v.w;
        }
        
        [JsonIgnore] public Vector4 Vector4 => new Vector4(x, y, z, w);
    }
}