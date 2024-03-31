using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item;
using Core.Item.Util;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.Generate
{
    public static class MachineIoGenerate
    {
        public static MachineIOTest[] MachineIoTestCase(Recipe recipe, int seed)
        {
            var testCase = new List<MachineIOTest>();

            var (_, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var recipes = recipe.Recipes;
            foreach (var r in recipes)
            {
                //必要量だけ入れる
                testCase.Add(new MachineIOTest(
                    itemStackFactory,
                    r.Input,
                    r.Output,
                    CreateEmptyItemStacksList.Create(r.Input.Length, itemStackFactory),
                    r.BlockID,
                    r.Time,
                    1));


                var random = new Random(seed);

                //inputにランダムな量増減する
                var input = r.Input.Select(rInput => new InputItem(rInput.ID, rInput.Count)).ToList();

                //ランダムな数足す
                input.ForEach(i => i.Count += random.Next(0, i.Count * 10));
                var remainder = new List<InputItem>();
                remainder.AddRange(input.Select(i => new InputItem(i.ID, i.Count)));

                var cnt = 0;
                var continue_ = true;
                //余りを算出する
                while (continue_)
                {
                    for (var j = 0; j < remainder.Count; j++)
                        if (remainder[j].Count < r.Input[j].Count)
                        {
                            continue_ = false;
                            break;
                        }

                    if (!continue_) break;

                    for (var j = 0; j < remainder.Count; j++) remainder[j].Count -= r.Input[j].Count;

                    if (continue_) cnt++;
                }

                //出力アイテムもクラフト回数分倍にする
                var output = MachineIOTest.Convart(r.Output, itemStackFactory)
                    .Select(i => i = itemStackFactory.Create(i.Id, i.Count * cnt)).ToList();
                //インプットアイテム数を増やしたテストケース
                testCase.Add(new MachineIOTest(itemStackFactory, input.ToArray(), output, remainder.ToArray(),
                    r.BlockID, r.Time, cnt));
            }

            return testCase.ToArray();
        }

        public class MachineIOTest
        {
            public int BlockId;
            public int CraftCnt;
            public List<IItemStack> Input;
            public List<IItemStack> InputRemainder;
            public List<IItemStack> Output;
            public int Time;

            public MachineIOTest(ItemStackFactory itemStackFactory, InputItem[] input, OutputItem[] output,
                List<IItemStack> inputRemainder, int blockId, int time, int craftCnt)
            {
                BlockId = blockId;
                Input = Convart(input, itemStackFactory);
                Output = Convart(output, itemStackFactory);
                InputRemainder = inputRemainder;
                Time = time;
                CraftCnt = craftCnt;
            }

            public MachineIOTest(ItemStackFactory itemStackFactory, InputItem[] input, List<IItemStack> output,
                InputItem[] inputRemainder, int blockId, int time, int craftCnt)
            {
                BlockId = blockId;
                Input = Convart(input, itemStackFactory);
                Output = output;
                InputRemainder = Convart(inputRemainder, itemStackFactory);
                Time = time;
                CraftCnt = craftCnt;
            }

            public static List<IItemStack> Convart(InputItem[] input, ItemStackFactory itemStackFactory)
            {
                var r = new List<IItemStack>();
                foreach (var i in input) r.Add(itemStackFactory.Create(i.ID, i.Count));

                var a = r.Where(i => i.Count != 0).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }

            public static List<IItemStack> Convart(OutputItem[] output, ItemStackFactory itemStackFactory)
            {
                var r = new List<IItemStack>();
                foreach (var o in output) r.Add(itemStackFactory.Create(o.ID, o.Count));

                var a = r.Where(i => i.Id != BlockConst.EmptyBlockId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
    }
}