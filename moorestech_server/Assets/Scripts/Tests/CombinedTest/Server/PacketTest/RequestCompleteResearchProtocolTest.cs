using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Action;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.Research.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ResearchModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    [TestFixture]
    public class RequestCompleteResearchProtocolTest
    {
        private ServiceProvider _serviceProvider;
        private RequestCompleteResearchProtocol _protocol;
        private TestResearchDataStore _researchDataStore;

        [SetUp]
        public void SetUp()
        {
            var serviceCollection = new ServiceCollection();
            _researchDataStore = new TestResearchDataStore();
            serviceCollection.AddSingleton<IResearchDataStore>(_researchDataStore);

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _protocol = new RequestCompleteResearchProtocol(_serviceProvider);
        }

        [Test]
        public void RequestCompleteResearch_研究が完了可能な場合_成功レスポンスを返す()
        {
            // Arrange
            var researchGuid = Guid.NewGuid();
            var playerId = 1;
            _researchDataStore.SetCanCompleteResearch(researchGuid, playerId, true);
            _researchDataStore.SetCompletionResult(new ResearchCompletionResult
            {
                Success = true,
                CompletedResearchGuid = researchGuid
            });

            var request = new RequestCompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid);
            var payload = MessagePackSerializer.Serialize(request).ToList();

            // Act
            var response = _protocol.GetResponse(payload) as RequestCompleteResearchProtocol.ResponseCompleteResearchMessagePack;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.AreEqual(researchGuid.ToString(), response.CompletedResearchGuidStr);
            Assert.IsNull(response.ErrorMessage);
        }

        [Test]
        public void RequestCompleteResearch_研究が完了不可能な場合_失敗レスポンスを返す()
        {
            // Arrange
            var researchGuid = Guid.NewGuid();
            var playerId = 1;
            _researchDataStore.SetCanCompleteResearch(researchGuid, playerId, false);
            _researchDataStore.SetCompletionResult(new ResearchCompletionResult
            {
                Success = false,
                Reason = "Required items not found"
            });

            var request = new RequestCompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid);
            var payload = MessagePackSerializer.Serialize(request).ToList();

            // Act
            var response = _protocol.GetResponse(payload) as RequestCompleteResearchProtocol.ResponseCompleteResearchMessagePack;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            Assert.IsNull(response.CompletedResearchGuidStr);
            Assert.AreEqual("Required items not found", response.ErrorMessage);
        }

        private class TestResearchDataStore : IResearchDataStore
        {
            private readonly Dictionary<(Guid, int), bool> _canCompleteResults = new();
            private ResearchCompletionResult _completionResult;
            private readonly HashSet<Guid> _completedResearches = new();

            public void SetCanCompleteResearch(Guid researchGuid, int playerId, bool canComplete)
            {
                _canCompleteResults[(researchGuid, playerId)] = canComplete;
            }

            public void SetCompletionResult(ResearchCompletionResult result)
            {
                _completionResult = result;
            }

            public bool IsResearchCompleted(Guid researchGuid)
            {
                return _completedResearches.Contains(researchGuid);
            }

            public bool CanCompleteResearch(Guid researchGuid, int playerId)
            {
                return _canCompleteResults.TryGetValue((researchGuid, playerId), out var result) && result;
            }

            public ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId)
            {
                if (_completionResult != null)
                {
                    if (_completionResult.Success)
                    {
                        _completedResearches.Add(researchGuid);
                    }
                    return _completionResult;
                }

                return new ResearchCompletionResult { Success = false, Reason = "Not configured" };
            }

            public HashSet<Guid> GetCompletedResearchGuids()
            {
                return new HashSet<Guid>(_completedResearches);
            }

            public ResearchSaveJsonObject GetSaveJsonObject()
            {
                return new ResearchSaveJsonObject();
            }

            public void LoadResearchData(ResearchSaveJsonObject saveData)
            {
            }
        }
    }
}