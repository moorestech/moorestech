using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Challenge;
using Game.Challenge.Task.Factory;
using Game.UnlockState;
using UnityEngine;
using Game.Challenge.Task;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using static Server.Protocol.PacketResponse.GetChallengeInfoProtocol;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public abstract class GetChallengeInfoProtocolTestBase
    {
        protected const string Challenge1Guid = "00000000-0000-0000-4567-000000000001";
        protected const string Challenge2Guid = "00000000-0000-0000-4567-000000000002";
        protected const string Challenge3Guid = "00000000-0000-0000-4567-000000000003";
        protected const string Challenge4Guid = "00000000-0000-0000-4567-000000000004";
        protected const string Challenge5Guid = "00000000-0000-0000-4567-000000000005";
        protected const string EquipItemChallengeGuid = "00000000-0000-0000-4567-000000000102";
        
        protected const string Category1Guid = "03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1";
        protected const string Category2Guid = "35330f9d-f44f-493d-a6bc-07ae6413d7c4";
        protected const string Category2Challenge1Guid = "c2d84cfb-73cb-4ffa-99b5-9703c550f330";
    }
}