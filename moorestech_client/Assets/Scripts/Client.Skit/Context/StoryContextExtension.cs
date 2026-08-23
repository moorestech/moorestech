using System;
using Client.Skit.Define;
using Client.Skit.Localization;
using Client.Skit.Skit;
using Client.Skit.UI;
using UnityEngine;
using VContainer;

namespace Client.Skit.Context
{
    public static class StoryContextExtension
    {
        public static SkitUI GetSkitUI(this StoryContext storyContext) => storyContext.GetService<SkitUI>();
        public static BackgroundSkitUI GetBackgroundSkitUI(this StoryContext storyContext) => storyContext.GetService<BackgroundSkitUI>();
        
        public static VoiceDefine GetVoiceDefine(this StoryContext storyContext) => storyContext.GetService<VoiceDefine>();
        
        public static ISkitCamera GetSkitCamera(this StoryContext storyContext) => storyContext.GetService<ISkitCamera>();
        public static SkitOrigin GetSkitOrigin(this StoryContext storyContext) => storyContext.GetService<SkitOrigin>();

        public static ISkitLocalizationResolver GetLocalizationResolver(this StoryContext storyContext)
            => storyContext.GetService<ISkitLocalizationResolver>();


        public static SkitCharacter GetCharacter(this StoryContext storyContext, string characterId)
        {
            try
            {
                var container = storyContext.GetService<CharacterObjectContainer>();
                return container.GetCharacter(characterId);
            }
            catch (VContainerException e)
            {
                Debug.LogError("CharacterObjectContainerがコンテキストに設定されていませんでした。");
                return null;
            }
        }
    }
}
