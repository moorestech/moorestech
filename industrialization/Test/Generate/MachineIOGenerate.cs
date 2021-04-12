using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test.Generate
{
    public class  MachineIOGenerate
    {
        public static MachineIOTest[]  MachineIOTestCase(recipe recipe,int seed)
        {
            var testCase = new List<MachineIOTest>();
            recipes[] r = recipe.recipes;
            for (int i = 0; i < r.Length; i++)
            {
                //必要量だけ入れる
                testCase.Add(new MachineIOTest(i,
                    r[i].input,
                    r[i].output,
                    ItemStackFactory.CreateEmptyItemStacksArray(r[i].input.Length)));
                
                
                var random = new Random(seed);
                
                //inputにランダムな量増減する
                var input = r[i].input.Select(rInput => new inputitem(rInput.id, rInput.amount)).ToList();

                //1～4の乱数のうち1の時は引く、それ以外は足す
                if (random.Next(1, 5) == 1)
                {
                    input.ForEach(i => i.amount -= random.Next(0,i.amount));
                }
                else
                {
                    input.ForEach(i => i.amount += random.Next(0,i.amount*10));
                }
                var remainder = new List<inputitem>();
                remainder.AddRange(input.Select(i => new inputitem(i.id, i.amount)));

                var cnt = 0;
                var continue_ = true;
                //余りを算出する
                while (continue_)
                {
                    for (int j = 0; j < remainder.Count; j++)
                    {
                        if(remainder[j].amount < r[i].input[j].amount)
                        {
                            continue_ = false; 
                            break;
                        }
                        remainder[j].amount -= r[i].input[j].amount;
                    }
                    if(continue_) cnt++;
                }

                var output = MachineIOTest.Convart(r[i].output);
                output.ToList().ForEach(i => i = ItemStackFactory.NewItemStack(i.Id,i.Amount*cnt));
                testCase.Add(new MachineIOTest(i,input.ToArray(),output.ToArray(),remainder.ToArray()));
            }

            return testCase.ToArray();
        }
        
        public class MachineIOTest
        {
            public int recipeID;
            public IItemStack[] input;
            public IItemStack[] output;
            public IItemStack[] inputRemainder;

            public MachineIOTest(int recipeId, inputitem[] input, outputitem[] output, IItemStack[] inputRemainder)
            {
                recipeID = recipeId;
                this.input = Convart(input);
                this.output = Convart(output);
                this.inputRemainder = inputRemainder;
            }
            public MachineIOTest(int recipeId, inputitem[] input, IItemStack[] output, inputitem[] inputRemainder)
            {
                recipeID = recipeId;
                this.input = Convart(input);
                this.output = output;
                this.inputRemainder = Convart(inputRemainder);
            }

            public static IItemStack[] Convart(inputitem[] input)
            {
                var r = new List<IItemStack>();
                foreach (var i in input)
                {
                    r.Add(ItemStackFactory.NewItemStack(i.id,i.amount));
                }

                return r.ToArray();
            }

            public static IItemStack[] Convart(outputitem[] output)
            {
                var r = new List<IItemStack>();
                foreach (var o in output)
                {
                    r.Add(ItemStackFactory.NewItemStack(o.id,o.amount));
                }

                return r.ToArray();
            }
        }
    }
}