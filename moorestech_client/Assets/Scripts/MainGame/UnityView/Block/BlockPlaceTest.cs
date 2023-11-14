using System;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlaceTest : MonoBehaviour
    {
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private Vector2Int blockPos;
        [SerializeField] private Vector2Int blockSize;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                var a = SlopeBlockPlaceSystem.GetBlockFourCornerMaxHeight(blockPos, BlockDirection.North, blockSize);
                Debug.Log(a);
            }
        }
    }
}