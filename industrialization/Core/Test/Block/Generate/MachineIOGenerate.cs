using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Test.Block.Generate
{
    public static class MachineIoGenerate
    {
        public static MachineIOTest[]  MachineIoTestCase(recipe recipe,int seed)
        {
            var testCase = new List<MachineIOTest>();
            recipes[] recipes = recipe.recipes;
            foreach (var r in recipes)
            {
                //必要量だけ入れる
                testCase.Add(new MachineIOTest(
                    r.input,
                    r.output,
                    CreateEmptyItemStacksList.Create(r.input.Length),
                    r.BlockID,
                    r.time,
                    1));
                
                
                var random = new Random(seed);
                
                //inputにランダムな量増減する
                var input = r.input.Select(rInput => new inputitem(rInput.id, rInput.amount)).ToList();

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
                        if(remainder[j].amount < r.input[j].amount)
                        {
                            continue_ = false; 
                            break;
                        }
                    }
                    if(!continue_) break;
                    
                    for (int j = 0; j < remainder.Count; j++)
                    {
                        remainder[j].amount -= r.input[j].amount;
                    }
                    if(continue_) cnt++;
                }

                //出力アイテムもクラフト回数分倍にする
                var output = MachineIOTest.Convart(r.output).Select(i => i = ItemStackFactory.NewItemStack(i.Id,i.Amount*cnt)).ToList();
                //インプットアイテム数を増やしたテストケース
                testCase.Add(new MachineIOTest(input.ToArray(),output,remainder.ToArray(),r.BlockID,r.time,cnt));
            }

            return testCase.ToArray();
        }
        
        public class MachineIOTest
        {
            public int installtionId;
            public List<IItemStack> input;
            public List<IItemStack> output;
            public List<IItemStack> inputRemainder;
            public int time;
            public int CraftCnt;

            public MachineIOTest(inputitem[] input, outputitem[] output, List<IItemStack> inputRemainder,int installtionId,int time,int craftCnt)
            {
                this.installtionId = installtionId;
                this.input = Convart(input);
                this.output = Convart(output);
                this.inputRemainder = inputRemainder;
                this.time = time;
                CraftCnt = craftCnt;
            }
            public MachineIOTest(inputitem[] input, List<IItemStack> output, inputitem[] inputRemainder,int installtionId,int time,int craftCnt)
            {
                this.installtionId = installtionId;
                this.input = Convart(input);
                this.output = output;
                this.inputRemainder = Convart(inputRemainder);
                this.time = time;
                CraftCnt = craftCnt;
            }

            public static List<IItemStack> Convart(inputitem[] input)
            {
                var r = new List<IItemStack>();
                foreach (var i in input)
                {
                    r.Add(ItemStackFactory.NewItemStack(i.id,i.amount));
                }

                var a = r.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }

            public static List<IItemStack> Convart(outputitem[] output)
            {
                var r = new List<IItemStack>();
                foreach (var o in output)
                {
                    r.Add(ItemStackFactory.NewItemStack(o.id,o.amount));
                }

                var a = r.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
    }
}