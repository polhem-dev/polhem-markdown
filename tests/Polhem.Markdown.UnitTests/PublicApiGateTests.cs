using System.Reflection;

namespace Polhem.Markdown.UnitTests;

/// <summary>
/// No public API of the packages refers to a type of the engines they are built on, Markdig for parsing and AvaloniaEdit
/// for editing, so either can be upgraded, forked or replaced without a breaking change. The model the viewers render is
/// internal for the same reason.
/// </summary>
public class PublicApiGateTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] s_engineNamespaces = ["Markdig", "AvaloniaEdit"];

    [Theory(DisplayName = "No public type or member of a package refers to the Markdig or AvaloniaEdit namespace")]
    [MemberData(nameof(Packages.Names), MemberType = typeof(Packages))]
    public void PublicSurface_EachPackage_HasNoEngineType(string package)
    {
        var offenders = Assembly.Load(package).GetExportedTypes()
            .SelectMany(type => SurfaceOf(type).Where(entry => entry.Types.Any(IsEngineType)).Select(entry => entry.Name))
            .ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<(string Name, IEnumerable<Type> Types)> SurfaceOf(Type type)
    {
        yield return (type.FullName!, Enumerable.Repeat(type.BaseType, 1).OfType<Type>().Concat(type.GetInterfaces()));

        foreach (var member in type.GetMembers(Declared).Where(IsVisible))
        {
            yield return ($"{type.FullName}.{member.Name}", TypesOf(member));
        }
    }

    private static bool IsVisible(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        PropertyInfo property => property.GetAccessors(nonPublic: true).Any(IsVisible),
        EventInfo @event => @event.AddMethod is { } add && IsVisible(add),
        _ => false,
    };

    private static IEnumerable<Type> TypesOf(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
        FieldInfo field => [field.FieldType],
        PropertyInfo property => property.GetIndexParameters().Select(parameter => parameter.ParameterType).Append(property.PropertyType),
        EventInfo @event => Enumerable.Repeat(@event.EventHandlerType, 1).OfType<Type>(),
        _ => [],
    };

    private static bool IsEngineType(Type type)
    {
        if (type.HasElementType)
        {
            return IsEngineType(type.GetElementType()!);
        }
        if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericArguments().Any(IsEngineType))
        {
            return true;
        }
        return type.Namespace is { } ns
            && s_engineNamespaces.Any(engine => ns == engine || ns.StartsWith(engine + ".", StringComparison.Ordinal));
    }
}
