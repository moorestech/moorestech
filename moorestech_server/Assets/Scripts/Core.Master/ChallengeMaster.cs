using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster : IMasterValidator
    {
        public readonly Challenges Challenges;
        public ChallengeCategoryMasterElement[] ChallengeCategoryMasterElements => Challenges.Data;

        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeCategoryGuidMap;
        private Dictionary<Guid, ChallengeMasterElement> _challengeGuidMap;
        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeToCategoryMap;
        private Dictionary<Guid, List<Guid>> _nextChallenges;

        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
        }

        public bool Validate(out string errorLogs)
        {
            return ChallengeMasterUtil.Validate(Challenges, out errorLogs);
        }

        public void Initialize()
        {
            ChallengeMasterUtil.Initialize(Challenges, out _challengeCategoryGuidMap, out _challengeGuidMap, out _challengeToCategoryMap, out _nextChallenges);
        }
        
        /// <summary>
        ///     ピンの狙い先指定を候補mapObjectGuid集合へ解決する（client/server共通の唯一の規則）
        ///     Resolves a pin target param into candidate mapObjectGuids; the single rule shared by client and server.
        /// </summary>
        public HashSet<Guid> ResolvePinTargets(MapObjectPinTutorialParam param)
        {
            if (!TryResolvePinTargets(param, out var pinTargets))
            {
                throw new InvalidOperationException($"Unknown pinTargetType: {param.PinTargetType}");
            }

            return pinTargets;
        }

        /// <summary>
        ///     未知の狙い先指定でも例外にせず解決可否を返す（マスタ検証は落ちずに報告する必要がある）
        ///     Reports whether the target param resolves instead of throwing, because master validation must report, not crash.
        /// </summary>
        public bool TryResolvePinTargets(MapObjectPinTutorialParam param, out HashSet<Guid> pinTargets)
        {
            switch (param.PinTargetParam)
            {
                case MapObjectPinTargetParam byMapObject:
                    pinTargets = new HashSet<Guid> { byMapObject.MapObjectGuid };
                    return true;
                // そのアイテムを落とす全mapObjectが候補。木の種類が増えてもマスタ側の列挙は不要
                // Every mapObject dropping the item is a candidate, so new tree species need no master enumeration
                case EarnItemPinTargetParam byEarnItem:
                    pinTargets = MasterHolder.MapObjectMaster.GetMapObjectGuidsByEarnItem(byEarnItem.ItemGuid);
                    return true;
                default:
                    pinTargets = new HashSet<Guid>();
                    return false;
            }
        }

        public List<ChallengeMasterElement> GetNextChallenges(Guid challengeGuid)
        {
            if (!_nextChallenges.TryGetValue(challengeGuid, out var nextChallenges))
            {
                throw new InvalidOperationException($"Next challenges not found. ChallengeGuid:{challengeGuid}");
            }
            
            return nextChallenges.ConvertAll(GetChallenge);
        }
        
        public ChallengeMasterElement GetChallenge(Guid guid)
        {
            return _challengeGuidMap[guid];
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategoryFromChallengeGuid(Guid guid)
        {
            return _challengeToCategoryMap[guid];
        }
        
        /// <summary>
        /// 指定されたカテゴリの初期チャレンジ（前提条件がないチャレンジ）を取得する
        /// </summary>
        public List<ChallengeMasterElement> GetCategoryInitialChallenges(Guid categoryGuid)
        {
            var category = ChallengeCategoryMasterElements.FirstOrDefault(c => c.CategoryGuid == categoryGuid);
            if (category == null) return new List<ChallengeMasterElement>();
            
            var initialChallenges = new List<ChallengeMasterElement>();
            foreach (var challengeElement in category.Challenges)
            {
                // 前提条件がないチャレンジを初期チャレンジとする
                if (challengeElement.PrevChallengeGuids == null || challengeElement.PrevChallengeGuids.Length == 0)
                {
                    initialChallenges.Add(challengeElement);
                }
            }
            
            return initialChallenges;
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategory(Guid categoryGuid)
        {
            return _challengeCategoryGuidMap[categoryGuid];
        }
    }
}