using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests
{
    public class CommonBlockPlacePointCalculatorTest
    {
        private readonly TestCase[] _testCases =
        {
            new() // 1ブロックだけ設置
            {
                PlaceStartPoint = new Vector3Int(0, 0, 0),
                PlaceEndPoint = new Vector3Int(0, 0, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 0, 0), BlockVerticalDirection.Horizontal),
                },
            },
            new() // 連続2ブロック設置 & 初期高さ + 1の場合
            {
                PlaceStartPoint = new Vector3Int(0, 0, 0),
                PlaceEndPoint = new Vector3Int(1, 1, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 0, 0), BlockVerticalDirection.Up),
                    (new Vector3Int(1, 1, 0), BlockVerticalDirection.Horizontal),
                },
            },
            new() // 連続2ブロック設置 & 初期高さ - 1の場合
            {
                PlaceStartPoint = new Vector3Int(0, 1, 0),
                PlaceEndPoint = new Vector3Int(1, 0, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 0, 0), BlockVerticalDirection.Down),
                    (new Vector3Int(1, 0, 0), BlockVerticalDirection.Horizontal),
                },
            },
            new() // 連続3ブロック設置 & 初期高さ - 1の場合
            {
                PlaceStartPoint = new Vector3Int(0, 1, 0),
                PlaceEndPoint = new Vector3Int(2, 0, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 1, 0), BlockVerticalDirection.Horizontal),
                    (new Vector3Int(1, 0, 0), BlockVerticalDirection.Down),
                    (new Vector3Int(2, 0, 0), BlockVerticalDirection.Horizontal),
                },
            },
            new() // 連続3ブロック設置 & 初期高さ + 2の場合
            {
                PlaceStartPoint = new Vector3Int(0, 0, 0),
                PlaceEndPoint = new Vector3Int(2, 2, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 1, 0), BlockVerticalDirection.Horizontal),
                    (new Vector3Int(1, 1, 0), BlockVerticalDirection.Up),
                    (new Vector3Int(2, 2, 0), BlockVerticalDirection.Horizontal),
                },
            },
            new() // 連続3ブロック設置 & 初期高さ - 2の場合
            {
                PlaceStartPoint = new Vector3Int(0, 2, 0),
                PlaceEndPoint = new Vector3Int(2, 0, 0),
                ExpectedPoints = new[]
                {
                    (new Vector3Int(0, 1, 0), BlockVerticalDirection.Horizontal),
                    (new Vector3Int(1, 0, 0), BlockVerticalDirection.Down),
                    (new Vector3Int(2, 0, 0), BlockVerticalDirection.Horizontal),
                },
            },
        };
        
        [Test]
        public void BlockPlaceTest()
        {
            for (var i = 0; i < _testCases.Length; i++)
            {
                var testCase = _testCases[i];
                
                // isStartDirectionZがどちらの場合でも同一の挙動を期待する
                
                Debug.Log($"TestCase: {i} startDirectionZ: true");
                BlockPlaceTest(testCase, true);
                Debug.Log("  Passed");
                
                Debug.Log($"TestCase: {i} startDirectionZ: false");
                BlockPlaceTest(testCase, false);
                Debug.Log("  Passed");
            }
        }
        
        private void BlockPlaceTest(TestCase testCase, bool isStartDirectionZ)
        {
            var blockMasterElement = new BlockMasterElement(
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                Guid.Empty,
                testCase.BlockSize,
                null,
                null,
                null,
                null,
                true
            );
            
            List<PlaceInfo> actual = CommonBlockPlacePointCalculator.CalculatePoint(
                testCase.PlaceStartPoint,
                testCase.PlaceEndPoint,
                isStartDirectionZ,
                BlockDirection.North,
                blockMasterElement,
                (_, _) => true
            );
            
            Assert.AreEqual(testCase.ExpectedPoints.Length, actual.Count);
            for (var i = 0; i < testCase.ExpectedPoints.Length; i++)
            {
                (Vector3Int position, BlockVerticalDirection? verticalDirection) expected = testCase.ExpectedPoints[i];
                
                Assert.AreEqual(expected.position, actual[i].Position);
                Assert.AreEqual(testCase.ExpectedPoints[i].verticalDirection, actual[i].VerticalDirection);
            }
        }
        
        private struct TestCase
        {
            public Vector3Int BlockSize;
            public Vector3Int PlaceStartPoint;
            public Vector3Int PlaceEndPoint;
            public (Vector3Int position, BlockVerticalDirection verticalDirection)[] ExpectedPoints;
        }
    }
}