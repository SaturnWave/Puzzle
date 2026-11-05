using System.Reflection;
using System.Reflection.Emit;

namespace Puzzle.Core.Tests;

/// <summary>
/// What compiled code reaches, read from its instructions: every method, field and type a method's body names. A test
/// of "this part of the code cannot do that" asks the assembly with it, where a search of the source text would miss a
/// call made through another name.
/// </summary>
internal static class IlReferences
{
    private const byte TwoByteOpCodePrefix = 0xFE;
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>Every member the methods and constructors declared by <paramref name="type"/> name in their bodies.</summary>
    public static IReadOnlyList<MemberInfo> MembersUsedBy(Type type) => type.GetMethods(Declared)
        .Cast<MethodBase>()
        .Concat(type.GetConstructors(Declared))
        .SelectMany(MembersUsedBy)
        .ToArray();

    /// <summary>
    /// Every type those members belong to or are: the type a called method or a read field is declared on, a type that
    /// is constructed, cast to or named, and the type arguments of all of them.
    /// </summary>
    public static IReadOnlySet<Type> TypesUsedBy(Type type) => MembersUsedBy(type).SelectMany(TypesOf).ToHashSet();

    private static IEnumerable<MemberInfo> MembersUsedBy(MethodBase method)
    {
        byte[]? instructions = method.GetMethodBody()?.GetILAsByteArray();
        if (instructions is null)
        {
            yield break;
        }

        Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } generic ? generic.GetGenericArguments() : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        int position = 0;
        while (position < instructions.Length)
        {
            short value = instructions[position++];
            if (value == TwoByteOpCodePrefix)
            {
                value = unchecked((short)((TwoByteOpCodePrefix << 8) | instructions[position++]));
            }

            OpCode code = OpCodesByValue[value];
            if (code.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                int token = BitConverter.ToInt32(instructions, position);
                if (method.Module.ResolveMember(token, typeArguments, methodArguments) is { } member)
                {
                    yield return member;
                }
            }

            position += OperandSize(code.OperandType, instructions, position);
        }
    }

    private static int OperandSize(OperandType operand, byte[] instructions, int position) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(instructions, position)),
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or OperandType.InlineSig
            or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        _ => throw new NotSupportedException($"An instruction operand of kind {operand}."),
    };

    private static IEnumerable<Type> TypesOf(MemberInfo member) => member switch
    {
        Type type => WithArguments(type),
        MethodInfo { IsGenericMethod: true } method => WithArguments(method.DeclaringType).Concat(method.GetGenericArguments().SelectMany(WithArguments)),
        FieldInfo field => WithArguments(field.DeclaringType).Concat(WithArguments(field.FieldType)),
        _ => WithArguments(member.DeclaringType),
    };

    /// <summary>The type as it is defined, and every type it is made of: its element type and its type arguments.</summary>
    private static IEnumerable<Type> WithArguments(Type? type)
    {
        if (type is null || type.IsGenericParameter)
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (Type element in WithArguments(type.GetElementType()))
            {
                yield return element;
            }

            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (Type ofArgument in WithArguments(argument))
            {
                yield return ofArgument;
            }
        }
    }
}
