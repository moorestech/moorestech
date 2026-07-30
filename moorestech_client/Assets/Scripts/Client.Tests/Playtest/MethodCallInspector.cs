using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Client.Tests.Playtest
{
    internal static class MethodCallInspector
    {
        private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

        static MethodCallInspector()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var opCode = (OpCode)field.GetValue(null);
                var value = unchecked((ushort)opCode.Value);
                if (value < 0x100)
                    SingleByteOpCodes[value] = opCode;
                else if ((value & 0xff00) == 0xfe00)
                    MultiByteOpCodes[value & 0xff] = opCode;
            }
        }

        internal static bool ContainsCall(MethodInfo caller, MethodInfo callee)
        {
            return FindCallOffset(caller, callee, 0) >= 0;
        }

        internal static bool CallsInOrder(MethodInfo caller, MethodInfo firstCallee, MethodInfo secondCallee)
        {
            var firstCallOffset = FindCallOffset(caller, firstCallee, 0);
            if (firstCallOffset < 0) return false;

            return FindCallOffset(caller, secondCallee, firstCallOffset + 1) > firstCallOffset;
        }

        private static int FindCallOffset(MethodInfo caller, MethodInfo callee, int searchStartOffset)
        {
            var bytes = caller.GetMethodBody().GetILAsByteArray();
            var position = 0;
            while (position < bytes.Length)
            {
                var instructionOffset = position;
                var opCode = ReadOpCode(bytes, ref position);
                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(bytes, position);
                    var calledMethod = caller.Module.ResolveMethod(token, caller.DeclaringType.GetGenericArguments(), caller.GetGenericArguments());
                    if (instructionOffset >= searchStartOffset && calledMethod.Module == callee.Module && calledMethod.MetadataToken == callee.MetadataToken)
                        return instructionOffset;
                }
                position += GetOperandSize(opCode.OperandType, bytes, position);
            }
            return -1;
        }

        private static OpCode ReadOpCode(byte[] bytes, ref int position)
        {
            var firstByte = bytes[position++];
            if (firstByte != 0xfe) return SingleByteOpCodes[firstByte];
            return MultiByteOpCodes[bytes[position++]];
        }

        private static int GetOperandSize(OperandType operandType, byte[] bytes, int position)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return sizeof(int) + BitConverter.ToInt32(bytes, position) * sizeof(int);
                default:
                    throw new ArgumentOutOfRangeException(nameof(operandType), operandType, null);
            }
        }
    }
}
