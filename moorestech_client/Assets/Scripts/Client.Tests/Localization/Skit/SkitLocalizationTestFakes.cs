using System;
using System.Collections.Generic;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Tests.Localization.Skit
{
    public sealed class FakeSkitDictionaryLoader : ISkitLocalizationDictionaryLoader
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new();
        private readonly Dictionary<string, Queue<UniTaskCompletionSource<IReadOnlyDictionary<string, string>>>>
            _gates = new();
        private readonly Dictionary<string, int> _loadCounts = new();

        public void Set(string languageCode, string key, string value)
        {
            _values[languageCode] = new Dictionary<string, string> { { key, value } };
        }

        public UniTaskCompletionSource<IReadOnlyDictionary<string, string>> GateNext(
            string languageCode)
        {
            if (!_gates.TryGetValue(languageCode, out var queue))
            {
                queue = new Queue<UniTaskCompletionSource<IReadOnlyDictionary<string, string>>>();
                _gates.Add(languageCode, queue);
            }

            var gate = new UniTaskCompletionSource<IReadOnlyDictionary<string, string>>();
            queue.Enqueue(gate);
            return gate;
        }

        public int GetLoadCount(string languageCode)
        {
            return _loadCounts.TryGetValue(languageCode, out var count) ? count : 0;
        }

        public UniTask<IReadOnlyDictionary<string, string>> LoadAsync(string languageCode)
        {
            _loadCounts[languageCode] = GetLoadCount(languageCode) + 1;
            if (_gates.TryGetValue(languageCode, out var queue) && queue.Count > 0)
            {
                return queue.Dequeue().Task;
            }

            return UniTask.FromResult(_values[languageCode]);
        }
    }

    public sealed class FakeSkitLocalizationSource : ISkitLocalizationSource
    {
        private readonly Subject<Unit> _languageChanged = new();
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new();
        private string _currentLanguageCode = "japanese";

        public string GetCurrentLanguageCode()
        {
            return _currentLanguageCode;
        }

        public IObservable<Unit> GetLanguageChanged()
        {
            return _languageChanged;
        }

        public bool TryGetDictionary(
            string languageCode,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            return _values.TryGetValue(languageCode, out dictionary);
        }

        public void Set(string languageCode, string key, string value)
        {
            var values = _values.TryGetValue(languageCode, out var current)
                ? new Dictionary<string, string>(current)
                : new Dictionary<string, string>();
            values[key] = value;
            _values[languageCode] = values;
        }

        public void SetLanguage(string languageCode)
        {
            _currentLanguageCode = languageCode;
            _languageChanged.OnNext(Unit.Default);
        }

        public void PublishDictionaryChange()
        {
            _languageChanged.OnNext(Unit.Default);
        }

        public SkitCharacterLocalizationIdentity GetCharacterIdentity(string characterId)
        {
            return new SkitCharacterLocalizationIdentity(
                "character.01234567-89ab-cdef-0123-456789abcdef.name",
                "Source Character");
        }
    }
}
