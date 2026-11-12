using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Runtime.InteropServices;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class ReflectionMetadataProvider(
    ISystemClock? clock = null,
    ReflectionMetadataOptions? options = null) : IMetadataProvider
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const string CompilerGeneratedAttributeName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private const string FlagsAttributeName = "System.FlagsAttribute";

    private readonly ReflectionMetadataOptions _options = options ?? new ReflectionMetadataOptions();

    public Task<AssemblyModel> LoadAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The reflection metadata provider requires an assembly file path.");

    public async Task<AssemblyModel> LoadAsync(MetadataSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.IsFile)
        {
            throw new InvalidOperationException("The reflection metadata provider requires an assembly file path.");
        }

        var filePath = ResolvePath(source.FilePath!);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Assembly file not found: {filePath}", filePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var bytes = await ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var loadedAt = clock?.Now ?? DateTimeOffset.Now;

        return await Task.Run(() => Read(bytes, filePath, loadedAt, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolvePath(string filePath)
    {
        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"The assembly path is not valid: {filePath}", exception);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new InvalidDataException($"The assembly file could not be read: {filePath}", exception);
        }
    }

    private AssemblyModel Read(byte[] bytes, string filePath, DateTimeOffset loadedAt, CancellationToken cancellationToken)
    {
        using var context = CreateContext(filePath);
        var assembly = Load(context, bytes, filePath);

        var identity = assembly.GetName();
        var namespaces = ReadNamespaces(assembly, cancellationToken);

        return new AssemblyModel(
            identity.Name ?? Path.GetFileNameWithoutExtension(filePath),
            identity.Version?.ToString() ?? "0.0.0.0",
            Path.GetFileName(filePath),
            namespaces,
            loadedAt,
            filePath,
            AssemblyLocation.File,
            RuntimeVersion: ReadRuntimeVersion(assembly))
        {
            FullName = identity.FullName,
            Culture = string.IsNullOrEmpty(identity.CultureName) ? "neutral" : identity.CultureName,
            PublicKeyToken = DescribePublicKeyToken(identity.GetPublicKeyToken()),
        };
    }

    private MetadataLoadContext CreateContext(string filePath)
    {
        var paths = new List<string>(ResolveProbingPaths(filePath));
        paths.AddRange(_options.ProbingPaths);
        paths.AddRange(RuntimeAssemblies());

        var resolver = new PathAssemblyResolver(paths.Distinct(StringComparer.OrdinalIgnoreCase));

        return new MetadataLoadContext(resolver, _options.CoreAssemblyName);
    }

    private IEnumerable<string> ResolveProbingPaths(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        if (!_options.ProbeAssemblyDirectory)
        {
            return [filePath];
        }

        return Directory
            .EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
            .Prepend(filePath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> RuntimeAssemblies()
    {
        var directory = RuntimeEnvironment.GetRuntimeDirectory();

        return string.IsNullOrEmpty(directory) || !Directory.Exists(directory)
            ? []
            : Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
    }

    private static Assembly Load(MetadataLoadContext context, byte[] bytes, string filePath)
    {
        try
        {
            return context.LoadFromByteArray(bytes);
        }
        catch (BadImageFormatException exception)
        {
            throw new InvalidDataException($"File is not a valid .NET assembly: {filePath}", exception);
        }
        catch (FileLoadException exception)
        {
            throw new InvalidDataException($"Assembly could not be loaded: {filePath}", exception);
        }
    }

    private static string? ReadRuntimeVersion(Assembly assembly)
    {
        try
        {
            return assembly.ImageRuntimeVersion;
        }
        catch (Exception exception) when (exception is FileNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    private List<NamespaceModel> ReadNamespaces(Assembly assembly, CancellationToken cancellationToken)
    {
        var buckets = new Dictionary<string, List<ClassModel>>(StringComparer.Ordinal);
        var namespaces = new List<NamespaceModel>();

        foreach (var type in EnumerateTypes(assembly, cancellationToken))
        {
            var @namespace = type.Namespace ?? string.Empty;

            if (!buckets.TryGetValue(@namespace, out var types))
            {
                types = [];
                buckets.Add(@namespace, types);
                namespaces.Add(new NamespaceModel(@namespace, types));
            }

            types.Add(ReadType(type, @namespace, cancellationToken));
        }

        return namespaces;
    }

    private IEnumerable<Type> EnumerateTypes(Assembly assembly, CancellationToken cancellationToken)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type is not null).ToArray()!;
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException or BadImageFormatException)
        {
            types = [];
        }

        foreach (var type in types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (type.FullName is null)
            {
                continue;
            }

            if (type.IsNested ? _options.IncludeNestedTypes : !type.IsSpecialName)
            {
                yield return type;
            }
        }
    }

    private ClassModel ReadType(Type type, string @namespace, CancellationToken cancellationToken)
    {
        var kind = ResolveKind(type);

        return new ClassModel(
            Name: type.Name,
            FullName: type.FullName ?? type.Name,
            BaseType: DescribeBaseType(type),
            Methods: kind == TypeKind.Enum ? [] : ReadMethods(type, cancellationToken),
            Properties: ReadProperties(type),
            Fields: ReadFields(type, kind),
            Constructors: kind == TypeKind.Enum ? [] : ReadConstructors(type),
            Visibility: ResolveVisibility(type),
            IsStatic: type is { IsAbstract: true, IsSealed: true } && kind == TypeKind.Class,
            IsAbstract: type.IsAbstract && kind is TypeKind.Class or TypeKind.Interface,
            IsInterface: kind == TypeKind.Interface,
            NamespaceName: @namespace,
            TypeKind: kind,
            EnumMembers: kind == TypeKind.Enum ? ReadEnumMembers(type) : [],
            Interfaces: ReadInterfaces(type),
            IsSealed: type.IsSealed && kind == TypeKind.Class,
            IsGeneric: type.IsGenericTypeDefinition,
            AssemblyName: type.Assembly.GetName().Name,
            GenericParameters: ReadGenericParameters(type),
            IsNested: type.IsNested,
            DeclaringTypeName: DescribeDeclaringType(type),
            IsFlagsEnum: kind == TypeKind.Enum && HasAttribute(type, FlagsAttributeName));
    }

    private static string? DescribeDeclaringType(Type type)
    {
        var declaring = type.DeclaringType;

        if (declaring is null)
        {
            return null;
        }

        var prefix = declaring.DeclaringType is not null
            ? DescribeDeclaringType(declaring) + "+"
            : string.IsNullOrEmpty(declaring.Namespace) ? string.Empty : declaring.Namespace + ".";

        return string.IsNullOrEmpty(prefix) ? declaring.Name : $"{prefix}{declaring.Name}";
    }

    private static TypeKind ResolveKind(Type type) =>
        type.IsEnum
            ? TypeKind.Enum
            : type.IsInterface
                ? TypeKind.Interface
                : IsDelegate(type)
                    ? TypeKind.Delegate
                    : type.IsValueType
                        ? TypeKind.Struct
                        : TypeKind.Class;

    private static bool IsDelegate(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, "System.MulticastDelegate", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? DescribeBaseType(Type type) =>
        type.BaseType is null || type.BaseType == typeof(ValueType) || type.BaseType == typeof(Enum)
            ? null
            : DescribeType(type.BaseType);

    private static ImmutableArray<string> ReadInterfaces(Type type)
    {
        try
        {
            return type.GetInterfaces()
                .Select(DescribeType)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToImmutableArray();
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return [];
        }
    }

    private static ImmutableArray<string> ReadMethodGenericParameters(MethodInfo method)
    {
        try
        {
            return method.IsGenericMethodDefinition
                ? method.GetGenericArguments().Select(argument => argument.Name).ToImmutableArray()
                : [];
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return [];
        }
    }

    private static ImmutableArray<string> ReadGenericParameters(Type type)
    {
        try
        {
            return type.IsGenericTypeDefinition
                ? type.GetGenericArguments().Select(argument => argument.Name).ToImmutableArray()
                : [];
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return [];
        }
    }

    private ImmutableArray<MethodModel> ReadMethods(Type type, CancellationToken cancellationToken) =>
        SafeMembers(() => type.GetMethods(DeclaredMembers))
            .Where(method => !method.IsSpecialName)
            .Where(method => _options.IncludeCompilerGeneratedMembers || !HasAttribute(method, CompilerGeneratedAttributeName))
            .Select(method =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return new MethodModel(
                    method.Name,
                    DescribeType(method.ReturnType),
                    ResolveVisibility(method),
                    method.IsStatic,
                    ReadParameters(method.GetParameters()),
                    method.IsAbstract,
                    method.IsVirtual && !method.IsFinal,
                    IsOverride(method),
                    HasAttribute(method, typeof(ExtensionAttribute).FullName!),
                    ReadMethodGenericParameters(method));
            })
            .ToImmutableArray();

    private static bool IsOverride(MethodInfo method)
    {
        try
        {
            return method.GetBaseDefinition() != method;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static ImmutableArray<ParameterModel> ReadParameters(ParameterInfo[] parameters) =>
        parameters
            .Select(parameter => new ParameterModel(
                parameter.Name ?? $"arg{parameter.Position.ToString(CultureInfo.InvariantCulture)}",
                DescribeType(parameter.ParameterType),
                parameter.IsOptional,
                parameter.Position,
                ReadDefaultValue(parameter),
                parameter.IsOut,
                parameter.IsIn,
                parameter.ParameterType.IsByRef && !parameter.IsOut && !parameter.IsIn))
            .ToImmutableArray();

    private static string? ReadDefaultValue(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue)
        {
            return null;
        }

        try
        {
            return parameter.RawDefaultValue is { } value ? DescribeDefaultValue(value) : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private ImmutableArray<PropertyModel> ReadProperties(Type type) =>
        SafeMembers(() => type.GetProperties(DeclaredMembers))
            .Where(property => _options.IncludeCompilerGeneratedMembers || !HasAttribute(property, CompilerGeneratedAttributeName))
            .Select(property => new PropertyModel(
                property.Name,
                DescribeType(property.PropertyType),
                property.GetMethod is not null,
                property.SetMethod is not null,
                ResolveVisibility(property),
                (property.GetMethod ?? property.SetMethod)?.IsStatic ?? false,
                property.GetIndexParameters().Length > 0,
                property.SetMethod is null ? null : ResolveVisibility(property.SetMethod)))
            .ToImmutableArray();

    private ImmutableArray<FieldModel> ReadFields(Type type, TypeKind kind) =>
        kind == TypeKind.Enum
            ? []
            : SafeMembers(() => type.GetFields(DeclaredMembers))
                .Where(field => _options.IncludeCompilerGeneratedMembers || !HasAttribute(field, CompilerGeneratedAttributeName))
                .Select(field => new FieldModel(
                    field.Name,
                    DescribeType(field.FieldType),
                    ResolveVisibility(field),
                    field.IsStatic,
                    field.IsInitOnly,
                    field.IsLiteral))
                .ToImmutableArray();

    private static ImmutableArray<ConstructorModel> ReadConstructors(Type type) =>
        SafeMembers(() => type.GetConstructors(DeclaredMembers))
            .Select(constructor => new ConstructorModel(
                ResolveVisibility(constructor),
                ReadParameters(constructor.GetParameters()),
                constructor.IsStatic))
            .ToImmutableArray();

    private static ImmutableArray<EnumMemberModel> ReadEnumMembers(Type type) =>
        SafeMembers(() => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Select(field =>
            {
                var numeric = ReadEnumValue(field);

                return new EnumMemberModel(
                    field.Name,
                    numeric?.ToString(CultureInfo.InvariantCulture),
                    numeric);
            })
            .ToImmutableArray();

    private static long? ReadEnumValue(FieldInfo field)
    {
        try
        {
            return field.GetRawConstantValue() is var raw && raw is not null
                ? Convert.ToInt64(raw, CultureInfo.InvariantCulture)
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static MemberVisibility ResolveVisibility(Type type) =>
        type.IsNested
            ? ResolveVisibility(type.IsNestedPublic, type.IsNestedFamily, type.IsNestedAssembly, type.IsNestedFamORAssem, type.IsNestedFamANDAssem)
            : type.IsPublic ? MemberVisibility.Public : MemberVisibility.Internal;

    private static MemberVisibility ResolveVisibility(MethodBase method) =>
        ResolveVisibility(method.IsPublic, method.IsFamily, method.IsAssembly, method.IsFamilyOrAssembly, method.IsFamilyAndAssembly);

    private static MemberVisibility ResolveVisibility(FieldInfo field) =>
        ResolveVisibility(field.IsPublic, field.IsFamily, field.IsAssembly, field.IsFamilyOrAssembly, field.IsFamilyAndAssembly);

    private static MemberVisibility ResolveVisibility(PropertyInfo property)
    {
        MethodInfo?[] accessors = [property.GetMethod, property.SetMethod];

        return accessors
            .OfType<MethodInfo>()
            .Select(ResolveVisibility)
            .DefaultIfEmpty(MemberVisibility.Private)
            .Min();
    }

    private static MemberVisibility ResolveVisibility(
        bool isPublic,
        bool isFamily,
        bool isAssembly,
        bool isFamilyOrAssembly,
        bool isFamilyAndAssembly) =>
        (isPublic, isFamilyOrAssembly, isFamily, isAssembly, isFamilyAndAssembly) switch
        {
            (true, _, _, _, _) => MemberVisibility.Public,
            (_, true, _, _, _) => MemberVisibility.ProtectedInternal,
            (_, _, true, _, _) => MemberVisibility.Protected,
            (_, _, _, true, _) => MemberVisibility.Internal,
            (_, _, _, _, true) => MemberVisibility.PrivateProtected,
            _ => MemberVisibility.Private,
        };

    private static T[] SafeMembers<T>(Func<T[]> read) where T : MemberInfo
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return [];
        }
    }

    private static bool HasAttribute(MemberInfo member, string attributeName)
    {
        try
        {
            return member.GetCustomAttributesData().Any(attribute =>
                string.Equals(attribute.AttributeType.FullName, attributeName, StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return false;
        }
    }

    private static string DescribeType(Type type)
    {
        try
        {
            return FormatType(type);
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return type.Name;
        }
    }

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return FormatType(type.GetElementType() ?? type);
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType() ?? type)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);

        return tick < 0
            ? name
            : $"{name[..tick]}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
    }

    private static string DescribeDefaultValue(object value) => value switch
    {
        string text => $"\"{text}\"",
        bool flag => flag ? "true" : "false",
        char character => $"'{character}'",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "default",
    };

    private static string DescribePublicKeyToken(byte[]? token) =>
        token is null or { Length: 0 }
            ? string.Empty
            : Convert.ToHexStringLower(token);
}
