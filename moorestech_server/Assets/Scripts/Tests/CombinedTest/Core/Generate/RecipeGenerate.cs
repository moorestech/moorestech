using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Tests.CombinedTest.Core.Generate
{
    public static class RecipeGenerate
    {
        public static Recipe MakeRecipe(int seed, int recipeNum)
        {
            var random = new Random(seed);
            var recipes = new List<Recipes>();
            for (var i = 0; i < recipeNum; i++) recipes.Add(new Recipes(random));
            
            return new Recipe(recipes.ToArray());
        }
    }
    
    [DataContract]
    public class Recipe
    {
        [DataMember(Name = "recipes")] public Recipes[] Recipes;
        
        public Recipe(Recipes[] recipes)
        {
            Recipes = recipes;
        }
    }
    
    [DataContract]
    public class Recipes
    {
        [DataMember(Name = "BlockID")] public int BlockID;
        [DataMember(Name = "input")] public InputItem[] Input;
        [DataMember(Name = "output")] public OutputItem[] Output;
        [DataMember(Name = "time")] public int Time;
        
        public Recipes(Random r)
        {
            var inputNumber = r.Next(1, 11);
            var tmpInput = new List<InputItem>();
            for (var i = 0; i < inputNumber; i++)
            {
                //IDが重複するときはIDを変更
                int id;
                do
                {
                    id = r.Next(1, 1001);
                }
                while (tmpInput.Find(x => x.ID == id) != null);
                
                tmpInput.Add(new InputItem(id, r.Next(1, 101)));
            }
            
            Input = tmpInput.ToArray();
            
            
            var outputnum = r.Next(1, 11);
            var tmpOutput = new List<OutputItem>();
            for (var i = 0; i < outputnum; i++)
            {
                //IDが重複するときはIDを変更
                var id = r.Next(1, 1001);
                while (tmpOutput.Find(x => x.ID == id) != null) id = r.Next(1, 1001);
                
                tmpOutput.Add(new OutputItem(id, r.Next(1, 101), 1));
            }
            
            Output = tmpOutput.ToArray();
            
            
            BlockID = r.Next(0, 101);
            Time = r.Next(1, 4) * 1000;
        }
    }
    
    [DataContract]
    public class InputItem
    {
        [DataMember(Name = "count")] public int Count;
        [DataMember(Name = "id")] public int ID;
        
        public InputItem(int id, int count)
        {
            ID = id;
            Count = count;
        }
    }
    
    [DataContract]
    public class OutputItem
    {
        [DataMember(Name = "count")] public int Count;
        [DataMember(Name = "id")] public int ID;
        [DataMember(Name = "percent")] public double Percent;
        
        public OutputItem(int id, int count, double percent)
        {
            ID = id;
            Count = count;
            Percent = percent;
        }
    }
}