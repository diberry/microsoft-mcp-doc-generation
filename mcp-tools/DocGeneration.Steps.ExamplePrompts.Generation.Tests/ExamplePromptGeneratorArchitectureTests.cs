using System.Reflection;
using System.Reflection.Emit;
using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ExamplePromptGeneratorArchitectureTests
{
    private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

    static ExamplePromptGeneratorArchitectureTests()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = unchecked((ushort)opCode.Value);
            if (value < 0x100)
            {
                SingleByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                MultiByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    [Fact]
    public void GetPromptParameters_CompiledMethodInvokesSharedParameterSortingHelper_Bug743()
    {
        var method = typeof(ExamplePromptGenerator).GetMethod(
            "GetPromptParameters",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(Tool), typeof(IReadOnlyList<ParameterManifestParameter>)],
            modifiers: null);

        Assert.NotNull(method);

        var sharedSortingCallCount = CountSharedParameterSortingCalls(method);

        Assert.Equal(2, sharedSortingCallCount);
    }

    private static int CountSharedParameterSortingCalls(MethodInfo method)
    {
        var body = method.GetMethodBody();
        Assert.NotNull(body);

        var il = body.GetILAsByteArray();
        Assert.NotNull(il);

        var module = method.Module;
        var count = 0;
        for (var offset = 0; offset < il.Length;)
        {
            var opCode = ReadOpCode(il, ref offset);
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, offset);
                var resolvedMethod = module.ResolveMethod(
                    token,
                    method.DeclaringType?.GetGenericArguments(),
                    method.GetGenericArguments());

                if (resolvedMethod is MethodInfo methodInfo
                    && IsSharedParameterSortingHelper(methodInfo))
                {
                    count++;
                }
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }

        return count;
    }

    private static bool IsSharedParameterSortingHelper(MethodInfo methodInfo)
    {
        var methodDefinition = methodInfo.IsGenericMethod
            ? methodInfo.GetGenericMethodDefinition()
            : methodInfo;

        return methodDefinition.DeclaringType == typeof(Shared.ParameterSorting)
            && methodDefinition.Name == nameof(Shared.ParameterSorting.SortByRequiredThenName);
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        var value = il[offset++];
        if (value != 0xfe)
        {
            return SingleByteOpCodes[value];
        }

        return MultiByteOpCodes[il[offset++]];
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget
                or OperandType.InlineField
                or OperandType.InlineI
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
            _ => throw new NotSupportedException($"Unsupported IL operand type '{operandType}'."),
        };
    }
}
