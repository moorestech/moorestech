#nullable enable
using System;

namespace Server.Boot.Args
{
    /// <summary>オプション名を宣言する簡易属性。例: [Option("--path", "-p")]</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OptionAttribute : Attribute
    {
        public string[] Names { get; }
        public bool IsFlag { get; }
        
        /// <param name="names">先頭の値がシリアライズ時の既定名</param>
        /// <param name="isFlag">bool プロパティを true/false 値付きではなくスイッチで扱うとき true</param>
        public OptionAttribute(bool isFlag = false, params string[] names)
        {
            if (names == null || names.Length == 0)
                throw new ArgumentException("At least one name is required.", nameof(names));
            Names = names;
            IsFlag = isFlag;
        }
    }
}