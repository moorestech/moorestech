namespace Tests.CombinedTest.Core
{
    public class SteamGearGeneratorTest
    {
        public void GenerateConsumeTest()
        {
            // SteamGearGeneratorを作成
            // 指定時間たったら液体が消えていることをテストする
            // その時に一定以上、一定以下のトルク、RPMが生成されていることを確認する
        }
        
        public void MaxGenerateTest()
        {
            // Maxになるまでの時間文分液体を供給し続ける
            // アップデート中、前回よりもRPM、トルクが増加していることを確認する
            // 最大になる時間になったときに、RPM、トルクが最大値になっていることを確認する
        }
    }
}