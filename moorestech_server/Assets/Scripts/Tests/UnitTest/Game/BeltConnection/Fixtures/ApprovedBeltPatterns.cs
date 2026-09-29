using System;
using System.Collections.Generic;

namespace Tests.UnitTest.Game.BeltConnection.Fixtures
{
    internal static class ApprovedBeltPatterns
    {
        // ID桁はUL/UR/LL/LR。0=空、1/2/3=正方向の水平/上り/下り、4/5/6=逆方向の水平/上り/下り
        // ID digits are UL/UR/LL/LR: 0 empty, 1/2/3 forward flat/up/down, 4/5/6 reverse flat/up/down
        private static readonly Dictionary<string, BeltExpectedConnection> ConnectedCases = CreateConnectedCases();
        private static readonly string[] TouchingStates = { "1345", "1246", "26", "35" };

        internal static IEnumerable<BeltPatternCase> All()
        {
            for (var ul = 0; ul < 7; ul++)
            for (var ur = 0; ur < 7; ur++)
            for (var ll = 0; ll < 7; ll++)
            for (var lr = 0; lr < 7; lr++)
            {
                var id = $"{ul}{ur}{ll}{lr}";
                var connections = ConnectedCases.TryGetValue(id, out var connection)
                    ? new[] { connection } : Array.Empty<BeltExpectedConnection>();
                // 接触状態をスロット別の固定表から読む
                // Read touching states from the fixed per-slot table
                var touching = new List<BeltTestSlot>();
                for (var slot = 0; slot < 4; slot++)
                    if (TouchingStates[slot].Contains(id[slot])) touching.Add((BeltTestSlot)slot);
                yield return new BeltPatternCase(id, connections, touching);
            }
        }

        private static Dictionary<string, BeltExpectedConnection> CreateConnectedCases()
        {
            var result = new Dictionary<string, BeltExpectedConnection>();
            // 承認済み462接続を有向ペアごとに固定する。他の配置は接続0本
            // Fix the 462 approved connections by directed pair; all other configurations have zero connections
            Add(BeltTestSlot.LL, BeltTestSlot.UR, @"
                0120 0121 0122 0123 0124 0125 0126 0220 0221 0222 0223 0224
                0225 0226 2120 2121 2122 2123 2124 2125 2126 2220 2221 2222
                2223 2224 2225 2226 6120 6121 6122 6123 6124 6125 6126 6220
                6221 6222 6223 6224 6225 6226
            ");
            Add(BeltTestSlot.UR, BeltTestSlot.LL, @"
                0460 0461 0462 0463 0464 0465 0466 0660 0661 0662 0663 0664
                0665 0666 2460 2461 2462 2463 2464 2465 2466 2660 2661 2662
                2663 2664 2665 2666 6460 6461 6462 6463 6464 6465 6466 6660
                6661 6662 6663 6664 6665 6666
            ");
            Add(BeltTestSlot.UL, BeltTestSlot.LR, @"
                1003 1013 1023 1033 1043 1053 1063 1303 1313 1323 1333 1343
                1353 1363 1503 1513 1523 1533 1543 1553 1563 3003 3013 3023
                3033 3043 3053 3063 3303 3313 3323 3333 3343 3353 3363 3503
                3513 3523 3533 3543 3553 3563
            ");
            Add(BeltTestSlot.UL, BeltTestSlot.UR, @"
                1100 1101 1102 1103 1104 1105 1106 1110 1111 1112 1113 1114
                1115 1116 1120 1121 1122 1123 1124 1125 1126 1130 1131 1132
                1133 1134 1135 1136 1140 1141 1142 1143 1144 1145 1146 1150
                1151 1152 1153 1154 1155 1156 1160 1161 1162 1163 1164 1165
                1166 1200 1201 1202 1203 1204 1205 1206 1210 1211 1212 1213
                1214 1215 1216 1220 1221 1222 1223 1224 1225 1226 1230 1231
                1232 1233 1234 1235 1236 1240 1241 1242 1243 1244 1245 1246
                1250 1251 1252 1253 1254 1255 1256 1260 1261 1262 1263 1264
                1265 1266 3100 3101 3102 3103 3104 3105 3106 3110 3111 3112
                3113 3114 3115 3116 3120 3121 3122 3123 3124 3125 3126 3130
                3131 3132 3133 3134 3135 3136 3140 3141 3142 3143 3144 3145
                3146 3150 3151 3152 3153 3154 3155 3156 3160 3161 3162 3163
                3164 3165 3166
            ");
            Add(BeltTestSlot.LR, BeltTestSlot.UL, @"
                4005 4015 4025 4035 4045 4055 4065 4305 4315 4325 4335 4345
                4355 4365 4505 4515 4525 4535 4545 4555 4565 5005 5015 5025
                5035 5045 5055 5065 5305 5315 5325 5335 5345 5355 5365 5505
                5515 5525 5535 5545 5555 5565
            ");
            Add(BeltTestSlot.UR, BeltTestSlot.UL, @"
                4400 4401 4402 4403 4404 4405 4406 4410 4411 4412 4413 4414
                4415 4416 4420 4421 4422 4423 4424 4425 4426 4430 4431 4432
                4433 4434 4435 4436 4440 4441 4442 4443 4444 4445 4446 4450
                4451 4452 4453 4454 4455 4456 4460 4461 4462 4463 4464 4465
                4466 4600 4601 4602 4603 4604 4605 4606 4610 4611 4612 4613
                4614 4615 4616 4620 4621 4622 4623 4624 4625 4626 4630 4631
                4632 4633 4634 4635 4636 4640 4641 4642 4643 4644 4645 4646
                4650 4651 4652 4653 4654 4655 4656 4660 4661 4662 4663 4664
                4665 4666 5400 5401 5402 5403 5404 5405 5406 5410 5411 5412
                5413 5414 5415 5416 5420 5421 5422 5423 5424 5425 5426 5430
                5431 5432 5433 5434 5435 5436 5440 5441 5442 5443 5444 5445
                5446 5450 5451 5452 5453 5454 5455 5456 5460 5461 5462 5463
                5464 5465 5466
            ");
            return result;

            #region Internal
            void Add(BeltTestSlot source, BeltTestSlot target, string ids)
            {
                foreach (var id in ids.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    result.Add(id, new BeltExpectedConnection(source, target));
            }
            #endregion
        }
    }
}
