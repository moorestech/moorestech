using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter
{
    public class InitializeScenePipeline : MonoBehaviour
    {
        private readonly List<List<IInitializeSequence>> _initializeSequences = new();
        
        
        private void Start()
        {
            
        }
        
        private async UniTask Initialize()
        {
            foreach (var sequence in _initializeSequences)
            {
                var tasks = Enumerable.Select(sequence, s => s.Process()).ToList();
                
                await tasks;
            }
        }
        
        
    }
}