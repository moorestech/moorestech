#if NET6_0
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Test.CombinedTest.Core.Generate
{
    public static class RecipeGenerate
    {
        public static recipe MakeRecipe(int seed, int recipeNum)
        {
            var random = new Random(seed);
            var recipes = new List<recipes>();
            for (var i = 0; i < recipeNum; i++) recipes.Add(new recipes(random));

            return new recipe(recipes.ToArray());
        }
    }

    [DataContract]
    public class recipe
    {
        [DataMember(Name = "recipes")] public recipes[] recipes;

        public recipe(recipes[] recipes)
        {
            this.recipes = recipes;
        }
    }

    [DataContract]
    public class recipes
    {
        [DataMember(Name = "BlockID")] public int BlockID;
        [DataMember(Name = "input")] public inputitem[] input;
        [DataMember(Name = "output")] public outputitem[] output;
        [DataMember(Name = "time")] public int time;

        public recipes(Random r)
        {
            var inputnum = r.Next(1, 11);
            var tmpInput = new List<inputitem>();
            for (var i = 0; i < inputnum; i++)
            {
                //IDID
                var id = 0;
                do
                {
                    id = r.Next(1, 1001);
                } while (tmpInput.Find(x => x.id == id) != null);

                tmpInput.Add(new inputitem(id, r.Next(1, 101)));
            }

            input = tmpInput.ToArray();


            var outputnum = r.Next(1, 11);
            var tmpOutput = new List<outputitem>();
            for (var i = 0; i < outputnum; i++)
            {
                //IDID
                var id = r.Next(1, 1001);
                while (tmpOutput.Find(x => x.id == id) != null) id = r.Next(1, 1001);

                tmpOutput.Add(new outputitem(id, r.Next(1, 101), 1));
            }

            output = tmpOutput.ToArray();


            BlockID = r.Next(0, 101);
            time = r.Next(1, 4) * 1000;
        }
    }

    [DataContract]
    public class inputitem
    {
        [DataMember(Name = "count")] public int count;
        [DataMember(Name = "id")] public int id;

        public inputitem(int id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }

    [DataContract]
    public class outputitem
    {
        [DataMember(Name = "count")] public int count;
        [DataMember(Name = "id")] public int id;
        [DataMember(Name = "percent")] public double percent;

        public outputitem(int id, int count, double percent)
        {
            this.id = id;
            this.count = count;
            this.percent = percent;
        }
    }
}
#endif