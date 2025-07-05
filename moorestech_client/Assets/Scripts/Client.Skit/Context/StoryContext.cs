using System.Collections.Generic;
using Client.Skit.Define;
using Client.Skit.Skit;
using Client.Skit.UI;
using UnityEngine;
using VContainer;

namespace Client.Skit.Context
{
    public class StoryContext
    {
        public T GetService<T>() => _resolver.Resolve<T>();
        private readonly IObjectResolver _resolver;
        
        public StoryContext(IObjectResolver resolver)
        {
            _resolver = resolver;
        }
    }
}