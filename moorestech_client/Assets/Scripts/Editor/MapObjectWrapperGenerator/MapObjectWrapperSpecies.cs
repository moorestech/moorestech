using System;

// 生成側と登録側で共有する種1件分
// One species entry, shared by the prefab factory and the Addressable registrar
[Serializable]
public class MapObjectWrapperSpecies
{
    public string prefabPath;
    public string kind;
    public string address;
    public string wrapperPath;
    public string mapObjectGuid;
    public string mapObjectName;
}
