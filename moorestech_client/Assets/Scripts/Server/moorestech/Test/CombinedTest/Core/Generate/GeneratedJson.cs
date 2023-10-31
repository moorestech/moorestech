#if NET6_0
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;

namespace Test.CombinedTest.Core.Generate
{
    /// <summary>
    ///     レシピファイル生成をする
    /// </summary>
    public class GeneratedJson
    {
        [Test]
        public void Json()
        {
            var seed = 2119350917;
            var recipeNum = 40;
            var recipe = RecipeGenerate.MakeRecipe(seed, recipeNum);

            // データをJSON形式にシリアル化して、メモリーストリームに出力する。
            var st = new MemoryStream(); // メモリーストリームを作成
            var serializer = new DataContractJsonSerializer(typeof(recipe)); // シリアライザーを作成
            serializer.WriteObject(st, recipe); // シリアライザーで出力

            // メモリーストリームの内容をコンソールに出力する。
            st.Position = 0;
            var reader = new StreamReader(st);
            Console.WriteLine(reader.ReadToEnd());


            Assert.True(true);
        }
    }
}
#endif