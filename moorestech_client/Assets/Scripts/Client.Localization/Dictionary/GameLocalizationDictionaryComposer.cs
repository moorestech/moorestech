using System.Collections.Generic;
using Core.Master;
using Mod.Loader;

namespace Client.Localization
{
    internal static class GameLocalizationDictionaryComposer
    {
        public static LocalizationDictionaryCandidate Compose(
            ModsResource modsResource,
            IReadOnlyList<ModId> orderedModIds,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            var candidate = VanillaLocalizationDictionaryFactory.Create();
            ModLocalizationMerger.Merge(modsResource, orderedModIds, candidate);

            // mod Sourceの後へMaster正本を重ね、空原文も欠落として確定する
            // Overlay canonical Master after mod Source and finalize empty sources as omissions
            MasterSourceTextOverlay.Apply(candidate, masterSourceTexts);
            return candidate;
        }
    }
}
