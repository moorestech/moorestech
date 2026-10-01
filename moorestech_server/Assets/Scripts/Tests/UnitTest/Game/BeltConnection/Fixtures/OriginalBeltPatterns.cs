using System;
using System.Collections.Generic;

namespace Tests.UnitTest.Game.BeltConnection.Fixtures
{
    internal static class OriginalBeltPatterns
    {
        // 元の256配置と25図の期待値を保持する。各行は配置ID:送出>受入、-は接続なし
        // Preserve the original 256 configurations and 25 diagrams: ID:source>target, with - for no connection
        private const string OriginalCases = @"
            0000:- 0001:- 0002:- 0003:- 0100:- 0101:- 0102:- 0103:-
            0200:- 0201:- 0202:- 0203:- 0300:- 0301:- 0302:- 0303:-
            0010:- 0011:- 0012:- 0013:- 0110:- 0111:- 0112:- 0113:-
            0210:- 0211:- 0212:- 0213:- 0310:- 0311:- 0312:- 0313:-
            0020:- 0021:- 0022:- 0023:- 0120:LL>UR 0121:LL>UR 0122:LL>UR 0123:LL>UR
            0220:LL>UR 0221:LL>UR 0222:LL>UR 0223:LL>UR 0320:- 0321:- 0322:- 0323:-
            0030:- 0031:- 0032:- 0033:- 0130:- 0131:- 0132:- 0133:-
            0230:- 0231:- 0232:- 0233:- 0330:- 0331:- 0332:- 0333:-
            1000:- 1001:- 1002:- 1003:UL>LR 1100:UL>UR 1101:UL>UR 1102:UL>UR 1103:UL>UR
            1200:UL>UR 1201:UL>UR 1202:UL>UR 1203:UL>UR 1300:- 1301:- 1302:- 1303:UL>LR
            1010:- 1011:- 1012:- 1013:UL>LR 1110:UL>UR 1111:UL>UR 1112:UL>UR 1113:UL>UR
            1210:UL>UR 1211:UL>UR 1212:UL>UR 1213:UL>UR 1310:- 1311:- 1312:- 1313:UL>LR
            1020:- 1021:- 1022:- 1023:UL>LR 1120:UL>UR 1121:UL>UR 1122:UL>UR 1123:UL>UR
            1220:UL>UR 1221:UL>UR 1222:UL>UR 1223:UL>UR 1320:- 1321:- 1322:- 1323:UL>LR
            1030:- 1031:- 1032:- 1033:UL>LR 1130:UL>UR 1131:UL>UR 1132:UL>UR 1133:UL>UR
            1230:UL>UR 1231:UL>UR 1232:UL>UR 1233:UL>UR 1330:- 1331:- 1332:- 1333:UL>LR
            2000:- 2001:- 2002:- 2003:- 2100:- 2101:- 2102:- 2103:-
            2200:- 2201:- 2202:- 2203:- 2300:- 2301:- 2302:- 2303:-
            2010:- 2011:- 2012:- 2013:- 2110:- 2111:- 2112:- 2113:-
            2210:- 2211:- 2212:- 2213:- 2310:- 2311:- 2312:- 2313:-
            2020:- 2021:- 2022:- 2023:- 2120:LL>UR 2121:LL>UR 2122:LL>UR 2123:LL>UR
            2220:LL>UR 2221:LL>UR 2222:LL>UR 2223:LL>UR 2320:- 2321:- 2322:- 2323:-
            2030:- 2031:- 2032:- 2033:- 2130:- 2131:- 2132:- 2133:-
            2230:- 2231:- 2232:- 2233:- 2330:- 2331:- 2332:- 2333:-
            3000:- 3001:- 3002:- 3003:UL>LR 3100:UL>UR 3101:UL>UR 3102:UL>UR 3103:UL>UR
            3200:- 3201:- 3202:- 3203:- 3300:- 3301:- 3302:- 3303:UL>LR
            3010:- 3011:- 3012:- 3013:UL>LR 3110:UL>UR 3111:UL>UR 3112:UL>UR 3113:UL>UR
            3210:- 3211:- 3212:- 3213:- 3310:- 3311:- 3312:- 3313:UL>LR
            3020:- 3021:- 3022:- 3023:UL>LR 3120:UL>UR 3121:UL>UR 3122:UL>UR 3123:UL>UR
            3220:- 3221:- 3222:- 3223:- 3320:- 3321:- 3322:- 3323:UL>LR
            3030:- 3031:- 3032:- 3033:UL>LR 3130:UL>UR 3131:UL>UR 3132:UL>UR 3133:UL>UR
            3230:- 3231:- 3232:- 3233:- 3330:- 3331:- 3332:- 3333:UL>LR
        ";

        private const string DiagramCases = @"
            1100:UL>UR 1200:UL>UR 1003:UL>LR 1103:UL>UR 1203:UL>UR 3100:UL>UR 3200:- 3003:UL>LR
            3103:UL>UR 3203:- 0120:LL>UR 0220:LL>UR 0023:- 0123:LL>UR 0223:LL>UR 1120:UL>UR
            1220:UL>UR 1023:UL>LR 1123:UL>UR 1223:UL>UR 3120:UL>UR 3220:- 3023:UL>LR 3123:UL>UR
            3223:-
        ";

        internal static IEnumerable<BeltPatternCase> Cases() => ReadRows(OriginalCases);
        internal static IEnumerable<BeltPatternCase> Diagrams() => ReadRows(DiagramCases);

        private static IEnumerable<BeltPatternCase> ReadRows(string rows)
        {
            foreach (var entry in rows.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = entry.Split(':');
                var connections = new List<BeltExpectedConnection>();
                if (fields[1] != "-")
                {
                    var pair = fields[1].Split('>');
                    connections.Add(new BeltExpectedConnection((BeltTestSlot)Enum.Parse(typeof(BeltTestSlot), pair[0]),
                        (BeltTestSlot)Enum.Parse(typeof(BeltTestSlot), pair[1])));
                }
                yield return new BeltPatternCase(fields[0], connections, Array.Empty<BeltTestSlot>());
            }
        }
    }
}
