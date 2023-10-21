#if NET6_0
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;

namespace Test.CombinedTest.Core.Generate
{
    /// <summary>
    ///     
    /// </summary>
    public class GeneratedJson
    {
        [Test]
        public void Json()
        {
            var seed = 2119350917;
            var recipeNum = 40;
            var recipe = RecipeGenerate.MakeRecipe(seed, recipeNum);

            // JSON。
            var st = new MemoryStream(); // 
            var serializer = new DataContractJsonSerializer(typeof(recipe)); // 
            serializer.WriteObject(st, recipe); // 

            // 。
            st.Position = 0;
            var reader = new StreamReader(st);
            Console.WriteLine(reader.ReadToEnd());


            Assert.True(true);
        }
    }
}
#endif