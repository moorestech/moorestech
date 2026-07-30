using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Client.Game.Skit.Localization
{
    public sealed class SkitLocalizationScope
    {
        public static readonly SkitLocalizationScope Empty = new(
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        private readonly IReadOnlyDictionary<string, string> _target;
        private readonly IReadOnlyDictionary<string, string> _english;

        public SkitLocalizationScope(
            Dictionary<string, string> target,
            Dictionary<string, string> english)
        {
            _target = new ReadOnlyDictionary<string, string>(target);
            _english = new ReadOnlyDictionary<string, string>(english);
        }

        public string Resolve(string key, string sourceText)
        {
            if (_target.TryGetValue(key, out var target) && !string.IsNullOrEmpty(target))
            {
                return target;
            }

            if (_english.TryGetValue(key, out var english) && !string.IsNullOrEmpty(english))
            {
                return english;
            }

            return sourceText;
        }
    }
}
