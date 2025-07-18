using System;
using VContainer;

namespace Client.Skit.Context
{
    public class StoryContext : IDisposable
    {
        public T GetService<T>() => _resolver.Resolve<T>();
        private readonly IObjectResolver _resolver;
        
        public StoryContext(IObjectResolver resolver)
        {
            _resolver = resolver;
        }
        public void Dispose()
        {
            _resolver?.Dispose();
        }
    }
}