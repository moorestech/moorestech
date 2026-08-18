using System;

// species-inventory.jsonの1species分。生成側とAddressable登録側で共有する
// One species entry of species-inventory.json, shared by the prefab factory and the Addressable registrar
[Serializable]
public class MapObjectWrapperSpecies
{
    public string key;
    public string prefabPath;
    public string kind;
    public string address;
    public string wrapperPath;
    public string mapObjectGuid;
    public string mapObjectName;
}
