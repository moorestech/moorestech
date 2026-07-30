using System.Collections.Generic;

namespace Client.Localization
{
    internal sealed class PublishedLocalizationDictionarySnapshot
    {
        public readonly long Revision;
        public readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Dictionaries;

        public PublishedLocalizationDictionarySnapshot(
            long revision,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictionaries)
        {
            Revision = revision;
            Dictionaries = dictionaries;
        }
    }
}
