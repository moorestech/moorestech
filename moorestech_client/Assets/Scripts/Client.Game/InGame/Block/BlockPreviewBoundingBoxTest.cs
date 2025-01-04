using System;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockPreviewBoundingBoxTest : MonoBehaviour
    {
        [SerializeField] BlockPreviewBoundingBox _blockPreviewBoundingBox;
        
        [SerializeField] private Vector3Int test;
        
        private void Update()
        {
            _blockPreviewBoundingBox.SetBoundingBox(test);
        }
    }
}