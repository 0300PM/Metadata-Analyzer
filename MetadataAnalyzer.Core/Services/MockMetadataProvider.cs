using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class MockMetadataProvider(ISystemClock? clock = null) : IMetadataProvider
{
    public const string AssemblyName = "SampleLibrary";
    public const string AssemblyVersion = "1.0.0.0";
    public const string AssemblyFileName = "SampleLibrary.dll";

    public Task<AssemblyModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var loadedAt = clock?.Now ?? DateTimeOffset.Now;
        return Task.FromResult(BuildAssembly(loadedAt));
    }

    public static AssemblyModel BuildAssembly(DateTimeOffset loadedAt = default) => new(
        AssemblyName,
        AssemblyVersion,
        AssemblyFileName,
        Namespaces:
        [
            new NamespaceModel("SampleLibrary.Core", [UserService(), ProductService()]),
            new NamespaceModel("SampleLibrary.Models", [User(), Product()]),
            new NamespaceModel("SampleLibrary.Interface", [IUserService(), Logger(), UserRole()]),
        ],
        LoadedAt: loadedAt == default ? DateTimeOffset.Now : loadedAt);

    private static ClassModel UserService() => new(
        Name: "UserService",
        FullName: "SampleLibrary.Core.UserService",
        BaseType: "object",
        Fields:
        [
            new FieldModel("_repository", "IUserRepository", MemberVisibility.Private, IsReadOnly: true),
            new FieldModel("_cache", "Dictionary<int, User>", MemberVisibility.Private, IsStatic: true),
        ],
        Properties:
        [
            new PropertyModel("Count", "int", CanWrite: false),
        ],
        Methods:
        [
            new MethodModel("GetUser", "User", Parameters: [new ParameterModel("id", "int")]),
            new MethodModel("CreateUser", "User", Parameters:
            [
                new ParameterModel("name", "string"),
                new ParameterModel("email", "string", IsOptional: true),
            ]),
            new MethodModel("DeleteUser", "bool", Parameters: [new ParameterModel("id", "int")]),
        ],
        Constructors:
        [
            new ConstructorModel(MemberVisibility.Public, [new ParameterModel("repository", "IUserRepository")]),
        ]);

    private static ClassModel ProductService() => new(
        Name: "ProductService",
        FullName: "SampleLibrary.Core.ProductService",
        BaseType: "object",
        Methods:
        [
            new MethodModel("GetProduct", "Product", Parameters: [new ParameterModel("id", "int")]),
            new MethodModel(
                "CalculateTotal",
                "decimal",
                MemberVisibility.Public,
                IsStatic: true,
                Parameters: [new ParameterModel("quantity", "int")]),
        ],
        Constructors: [new ConstructorModel()]);

    private static ClassModel User() => new(
        Name: "User",
        FullName: "SampleLibrary.Models.User",
        BaseType: "object",
        Properties:
        [
            new PropertyModel("Id", "int"),
            new PropertyModel("Name", "string"),
            new PropertyModel("Email", "string"),
        ],
        Fields: [new FieldModel("_createdAt", "DateTime", MemberVisibility.Private, IsReadOnly: true)],
        Methods: [new MethodModel("GetFullName", "string")],
        Constructors: [new ConstructorModel()]);

    private static ClassModel Product() => new(
        Name: "Product",
        FullName: "SampleLibrary.Models.Product",
        BaseType: "object",
        Properties:
        [
            new PropertyModel("Id", "int"),
            new PropertyModel("Name", "string"),
            new PropertyModel("Price", "decimal"),
        ],
        Methods: [new MethodModel("CalculateTax", "decimal")],
        Constructors: [new ConstructorModel()]);

    private static ClassModel IUserService() => new(
        Name: "IUserService",
        FullName: "SampleLibrary.Interface.IUserService",
        BaseType: null,
        Methods:
        [
            new MethodModel("GetUser", "User", Parameters: [new ParameterModel("id", "int")]),
            new MethodModel("CreateUser", "User", Parameters: [new ParameterModel("name", "string")]),
        ],
        Visibility: MemberVisibility.Public,
        IsInterface: true);

    private static ClassModel Logger() => new(
        Name: "Logger",
        FullName: "SampleLibrary.Interface.Logger",
        BaseType: "object",
        Fields:
        [
            new FieldModel("_level", "LogLevel", MemberVisibility.Private),
        ],
        Methods:
        [
            new MethodModel(
                "Write",
                "void",
                MemberVisibility.Public,
                IsStatic: true,
                Parameters: [new ParameterModel("message", "string")]),
        ],
        IsStatic: true,
        TypeKind: TypeKind.Struct);

    private static ClassModel UserRole() => new(
        Name: "UserRole",
        FullName: "SampleLibrary.Interface.UserRole",
        BaseType: "Enum",
        EnumMembers:
        [
            new EnumMemberModel("Guest", "0"),
            new EnumMemberModel("Member", "1"),
            new EnumMemberModel("Administrator", "2"),
        ],
        TypeKind: TypeKind.Enum);
}
