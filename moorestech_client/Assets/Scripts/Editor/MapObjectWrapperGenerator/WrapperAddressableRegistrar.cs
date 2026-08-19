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

    // この生成器が持つアドレス空間。末尾のスラッシュで既存のVanilla/Environment/Tree(Tree.prefab)やBushを巻き込まない
    // The address space this generator owns; the trailing slash keeps the existing Vanilla/Environment/Tree (Tree.prefab) and Bush out of it
    private static readonly string[] GeneratedAddressPrefixes = { "Vanilla/Environment/Tree/", "Vanilla/Environment/Rock/" };

    public static int RegisterAll(List<MapObjectWrapperSpecies> speciesList)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("AddressableAssetSettings is missing");

        var group = settings.FindGroup(GroupName);
        if (group == null) throw new InvalidOperationException($"addressable group not found: {GroupName}");

        // 途中失敗でも登録を欠落させないため、破壊操作(RemoveGeneratedEntries)より先に全species分のGUIDを解決し切る
        // Resolve every species' GUID before the destructive RemoveGeneratedEntries call, so a mid-run failure never leaves entries missing
        var resolvedGuids = new List<string>(speciesList.Count);
        foreach (var species in speciesList)
        {
            var assetGuid = AssetDatabase.AssetPathToGUID(species.wrapperPath);
            if (string.IsNullOrEmpty(assetGuid)) throw new InvalidOperationException($"wrapper prefab is not imported: {species.wrapperPath}");
            resolvedGuids.Add(assetGuid);
        }

        RemoveGeneratedEntries(settings);

        var entries = new List<AddressableAssetEntry>(speciesList.Count);
        for (var i = 0; i < speciesList.Count; i++)
        {
            var entry = settings.CreateOrMoveEntry(resolvedGuids[i], group, false, false);
            entry.address = speciesList[i].address;
            entries.Add(entry);
        }

        // 100件超の登録を1件ずつ通知すると重いので、変更通知は最後に1回だけ出す
        // Posting an event per entry would be heavy for a hundred-plus registrations, so notify once at the end
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true, true);
        AssetDatabase.SaveAssets();
        return entries.Count;
    }

    // 消えた・改名されたspeciesのエントリが残るとアドレスが実体の無いアセットを指し続けるので、登録前に生成空間を空にする
    // Entries of removed or renamed species would keep pointing at assets that no longer exist, so the generated space is emptied before registering
    private static void RemoveGeneratedEntries(AddressableAssetSettings settings)
    {
        var staleGuids = new List<string>();
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
                if (HasGeneratedPrefix(entry.address))
                    staleGuids.Add(entry.guid);
        }

        foreach (var staleGuid in staleGuids) settings.RemoveAssetEntry(staleGuid, false);

        #region Internal

        bool HasGeneratedPrefix(string address)
        {
            foreach (var prefix in GeneratedAddressPrefixes)
                if (address.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        #endregion
    }
}
