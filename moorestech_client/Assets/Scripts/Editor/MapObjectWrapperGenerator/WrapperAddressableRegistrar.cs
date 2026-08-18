using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

// 生成済みラッパープレハブをVanillaグループへ入れ、masterのaddressablePathと同じアドレスを振る
// Puts the generated wrapper prefabs into the Vanilla group under the very addresses the master's addressablePath names
public static class WrapperAddressableRegistrar
{
    private const string GroupName = "Vanilla Asset Group";

    public static int RegisterAll(List<MapObjectWrapperSpecies> speciesList)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("AddressableAssetSettings is missing");

        var group = settings.FindGroup(GroupName);
        if (group == null) throw new InvalidOperationException($"addressable group not found: {GroupName}");

        var entries = new List<AddressableAssetEntry>(speciesList.Count);
        foreach (var species in speciesList)
        {
            var assetGuid = AssetDatabase.AssetPathToGUID(species.wrapperPath);
            if (string.IsNullOrEmpty(assetGuid)) throw new InvalidOperationException($"wrapper prefab is not imported: {species.wrapperPath}");

            var entry = settings.CreateOrMoveEntry(assetGuid, group, false, false);
            entry.address = species.address;
            entries.Add(entry);
        }

        // 100件超の登録を1件ずつ通知すると重いので、変更通知は最後に1回だけ出す
        // Posting an event per entry would be heavy for a hundred-plus registrations, so notify once at the end
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true, true);
        AssetDatabase.SaveAssets();
        return entries.Count;
    }
}
