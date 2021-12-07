using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block;
using Core.Item;
using Core.Item.Config;
using Core.Item.Util;
using Core.Util;

namespace Test.CombinedTest.Core.Generate
{
    public static class MachineIoGenerate
    {
        public static MachineIOTest[]  MachineIoTestCase(recipe recipe,int seed)
        {
            var testCase = new List<MachineIOTest>();
            var itemStackFactory = new ItemStackFactory(new TestItemConfig());
            recipes[] recipes = recipe.recipes;
            foreach (var r in recipes)
            {
                //必要量だけ入れる
                testCase.Add(new MachineIOTest(
                    itemStackFactory,
                    r.input,
                    r.output,
                    CreateEmptyItemStacksList.Create(r.input.Length,itemStackFactory),
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
                var output = MachineIOTest.Convart(r.output,itemStackFactory).Select(i => i = itemStackFactory.Create(i.Id,i.Amount*cnt)).ToList();
                //インプットアイテム数を増やしたテストケース
                testCase.Add(new MachineIOTest(itemStackFactory,input.ToArray(),output,remainder.ToArray(),r.BlockID,r.time,cnt));
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

            public MachineIOTest(ItemStackFactory itemStackFactory,inputitem[] input, outputitem[] output, List<IItemStack> inputRemainder,int installtionId,int time,int craftCnt)
            {
                this.installtionId = installtionId;
                this.input = Convart(input,itemStackFactory);
                this.output = Convart(output,itemStackFactory);
                this.inputRemainder = inputRemainder;
                this.time = time;
                CraftCnt = craftCnt;
            }
            public MachineIOTest(ItemStackFactory itemStackFactory,inputitem[] input, List<IItemStack> output, inputitem[] inputRemainder,int installtionId,int time,int craftCnt)
            {
                this.installtionId = installtionId;
                this.input = Convart(input,itemStackFactory);
                this.output = output;
                this.inputRemainder = Convart(inputRemainder,itemStackFactory);
                this.time = time;
                CraftCnt = craftCnt;
            }

            public static List<IItemStack> Convart(inputitem[] input,ItemStackFactory itemStackFactory)
            {
                var r = new List<IItemStack>();
                foreach (var i in input)
                {
                    r.Add(itemStackFactory.Create(i.id,i.amount));
                }

                var a = r.Where(i => i.Id != ItemConst.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }

            public static List<IItemStack> Convart(outputitem[] output,ItemStackFactory itemStackFactory)
            {
                var r = new List<IItemStack>();
                foreach (var o in output)
                {
                    r.Add(itemStackFactory.Create(o.id,o.amount));
                }

                var a = r.Where(i => i.Id != BlockConst.BlockConst.NullBlockId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
    }
}