using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

var gameDir = args.Length > 0
    ? args[0]
    : @"D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64";

var loadContext = new PathAssemblyLoadContext(gameDir);
var assemblyPath = Path.Combine(gameDir, "sts2.dll");
var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

Type[] allTypes;
try
{
    allTypes = assembly.GetTypes();
}
catch (ReflectionTypeLoadException ex)
{
    Console.WriteLine("TYPE_LOAD_EXCEPTION");
    foreach (var loaderException in ex.LoaderExceptions)
    {
        Console.WriteLine(loaderException?.Message);
    }

    allTypes = ex.Types.Where(static type => type != null).Cast<Type>().ToArray();
}

if (args.Length > 3 && args[1] == "--il")
{
    var typeName = args[2];
    var methodName = args[3];
    var targetType = allTypes.FirstOrDefault(type => type?.FullName == typeName);
    if (targetType == null)
    {
        Console.WriteLine($"Type not found: {typeName}");
        return;
    }

    var targetMethod = targetType
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        .FirstOrDefault(method => method.Name == methodName);

    if (targetMethod == null)
    {
        Console.WriteLine($"Method not found: {typeName}.{methodName}");
        return;
    }

    Console.WriteLine($"=== IL {targetType.FullName}.{targetMethod.Name} ===");
    IlDumpHelper.DumpIl(targetMethod);
    return;
}

var typeFilters = args.Skip(1).ToArray();
var selectedTypes = allTypes
    .Where(static type => type.FullName != null)
    .Where(type => typeFilters.Length == 0 || typeFilters.Any(filter => type.FullName!.Contains(filter, StringComparison.OrdinalIgnoreCase)))
    .OrderBy(static type => type.FullName)
    .ToArray();

foreach (var type in selectedTypes)
{
    Console.WriteLine($"=== {type.FullName} ===");
    Console.WriteLine("Fields:");
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
    {
        Console.WriteLine($"  {field.FieldType.Name} {field.Name}");
    }

    Console.WriteLine("Properties:");
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
    {
        Console.WriteLine($"  {property.PropertyType.Name} {property.Name}");
    }

    Console.WriteLine("Methods:");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(static method => method.Name))
    {
        var parameters = string.Join(", ", method.GetParameters().Select(static parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));
        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
    }

    Console.WriteLine();
}

sealed class PathAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _baseDirectory;

    public PathAssemblyLoadContext(string baseDirectory)
        : base(isCollectible: true)
    {
        _baseDirectory = baseDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidatePath = Path.Combine(_baseDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(candidatePath) == true)
        {
            return LoadFromAssemblyPath(candidatePath);
        }

        return null;
    }
}

static class IlDumpHelper
{
    public static void DumpIl(MethodInfo method)
    {
        var body = method.GetMethodBody();
        if (body == null)
        {
            Console.WriteLine("No method body");
            return;
        }

        var il = body.GetILAsByteArray();
        if (il == null)
        {
            Console.WriteLine("No IL bytes");
            return;
        }

        var module = method.Module;
        var singleByteOpCodes = new Dictionary<byte, OpCode>();
        var doubleByteOpCodes = new Dictionary<byte, OpCode>();

        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = unchecked((ushort)opCode.Value);
            if (value <= 0xFF)
            {
                singleByteOpCodes[(byte)value] = opCode;
            }
            else if ((value & 0xFF00) == 0xFE00)
            {
                doubleByteOpCodes[(byte)(value & 0xFF)] = opCode;
            }
        }

        var position = 0;
        while (position < il.Length)
        {
            var offset = position;
            OpCode opCode;
            var code = il[position++];
            if (code == 0xFE)
            {
                opCode = doubleByteOpCodes[il[position++]];
            }
            else
            {
                opCode = singleByteOpCodes[code];
            }

            object? operand = null;
            var operandSize = 0;

            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    operand = il[position];
                    operandSize = 1;
                    break;
                case OperandType.InlineVar:
                    operand = BitConverter.ToUInt16(il, position);
                    operandSize = 2;
                    break;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    operand = BitConverter.ToInt32(il, position);
                    operandSize = 4;
                    break;
                case OperandType.InlineI8:
                    operand = BitConverter.ToInt64(il, position);
                    operandSize = 8;
                    break;
                case OperandType.ShortInlineR:
                    operand = BitConverter.ToSingle(il, position);
                    operandSize = 4;
                    break;
                case OperandType.InlineR:
                    operand = BitConverter.ToDouble(il, position);
                    operandSize = 8;
                    break;
                case OperandType.ShortInlineBrTarget:
                    operand = (sbyte)il[position];
                    operandSize = 1;
                    break;
                default:
                    operand = "?";
                    break;
            }

            position += operandSize;

            string renderedOperand = string.Empty;
            if (operand != null)
            {
                renderedOperand = operand.ToString() ?? string.Empty;
                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    try
                    {
                        renderedOperand = module.ResolveMethod((int)operand).ToString() ?? renderedOperand;
                    }
                    catch {}
                }
                else if (opCode.OperandType == OperandType.InlineField)
                {
                    try
                    {
                        renderedOperand = module.ResolveField((int)operand).ToString() ?? renderedOperand;
                    }
                    catch {}
                }
                else if (opCode.OperandType == OperandType.InlineType)
                {
                    try
                    {
                        renderedOperand = module.ResolveType((int)operand).ToString() ?? renderedOperand;
                    }
                    catch {}
                }
                else if (opCode.OperandType == OperandType.InlineString)
                {
                    try
                    {
                        renderedOperand = $"\"{module.ResolveString((int)operand)}\"";
                    }
                    catch {}
                }
            }

            Console.WriteLine($"{offset:X4}: {opCode.Name} {renderedOperand}".TrimEnd());
        }
    }
}
