using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Test.Generate
{
    public static class MachineIoGenerate
    {
        public static MachineIOTest[]  MachineIoTestCase(recipe recipe,int seed)
        {
            var testCase = new List<MachineIOTest>();
            recipes[] r = recipe.recipes;
            for (int i = 0; i < r.Length; i++)
            {
                //必要量だけ入れる
                testCase.Add(new MachineIOTest(i,
                    r[i].input,
                    r[i].output,
                    CreateEmptyItemStacksList.Create(r[i].input.Length),
                    r[i].installationID,
                    r[i].time));
                
                
                var random = new Random(seed);
                
                //inputにランダムな量増減する
                var input = r[i].input.Select(rInput => new inputitem(rInput.id, rInput.amount)).ToList();

                //ランダムな数足す
                input.ForEach(i => i.amount += random.Next(0,i.amount*10));
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
                testCase.Add(new MachineIOTest(i,input.ToArray(),output,remainder.ToArray(),r[i].installationID,r[i].time));
            }

            return testCase.ToArray();
        }
        
        public class MachineIOTest
        {
            public int recipeID;
            public int installtionId;
            public List<IItemStack> input;
            public List<IItemStack> output;
            public List<IItemStack> inputRemainder;
            public int time;

            public MachineIOTest(int recipeId, inputitem[] input, outputitem[] output, List<IItemStack> inputRemainder,int installtionId,int time)
            {
                this.installtionId = installtionId;
                recipeID = recipeId;
                this.input = Convart(input);
                this.output = Convart(output);
                this.inputRemainder = inputRemainder;
                this.time = time;
            }
            public MachineIOTest(int recipeId, inputitem[] input, List<IItemStack> output, inputitem[] inputRemainder,int installtionId,int time)
            {
                this.installtionId = installtionId;
                recipeID = recipeId;
                this.input = Convart(input);
                this.output = output;
                this.inputRemainder = Convart(inputRemainder);
                this.time = time;
            }

            public static List<IItemStack> Convart(inputitem[] input)
            {
                var r = new List<IItemStack>();
                foreach (var i in input)
                {
                    r.Add(ItemStackFactory.NewItemStack(i.id,i.amount));
                }

                return r;
            }

            public static List<IItemStack> Convart(outputitem[] output)
            {
                var r = new List<IItemStack>();
                foreach (var o in output)
                {
                    r.Add(ItemStackFactory.NewItemStack(o.id,o.amount));
                }

                return r;
            }
        }
    }
}