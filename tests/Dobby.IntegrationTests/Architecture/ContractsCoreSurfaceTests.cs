using System.Reflection;
using System.Runtime.CompilerServices;
using Dobby.Contracts.Events;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ContractsCoreSurfaceTests
{
    private static readonly HashSet<string> AllowedCoreTypes = new(StringComparer.Ordinal)
    {
        "Dobby.Core.Result",
        "Dobby.Core.Result`1",
        "Dobby.Core.ErrorCode",
        "Dobby.Core.CorrelationId",
        "Dobby.Core.ICorrelationIdGenerator",
        "Dobby.Core.ICorrelationContextAccessor",
        "Dobby.Core.Versioning.HostVersion",
        "Dobby.Core.Versioning.IHostVersionProvider"
    };

    private static readonly HashSet<string> WpfAssemblyNames = new(StringComparer.Ordinal)
    {
        "PresentationFramework",
        "WindowsBase",
        "System.Xaml"
    };

    [Fact]
    public void Contracts_assembly_references_only_allowed_core_types()
    {
        var violations = new List<string>();

        foreach (var type in ContractsTypes())
        {
            InspectTypeSurface(type, violations);
        }

        violations
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBeEmpty("ADR-0001 allows Contracts to expose only the documented Dobby.Core primitives.");
    }

    [Fact]
    public void Contracts_assembly_has_no_wpf_or_ui_dependency()
    {
        var violations = ContractsAssembly()
            .GetReferencedAssemblies()
            .Where(reference => WpfAssemblyNames.Contains(reference.Name ?? string.Empty))
            .Select(reference => $"Dobby.Contracts assembly references WPF/UI assembly {reference.Name}.")
            .ToList();

        foreach (var type in ContractsTypes())
        {
            InspectWpfSurface(type, violations);
        }

        violations
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBeEmpty("Plugin-facing Contracts APIs must not expose WPF or UI-only types.");
    }

    private static void InspectTypeSurface(Type type, List<string> violations)
    {
        InspectCoreReference(type.BaseType, $"{type.FullName}.base", violations);
        InspectCustomAttributes(type, $"{type.FullName}.attributes", violations);

        foreach (var genericArgument in type.GetGenericArguments())
        {
            InspectCoreReference(genericArgument, $"{type.FullName}.generic-parameter", violations);
        }

        foreach (var @interface in type.GetInterfaces())
        {
            InspectCoreReference(@interface, $"{type.FullName}.interface", violations);
        }

        foreach (var field in ApiFields(type))
        {
            InspectCoreReference(field.FieldType, $"{type.FullName}.{field.Name}", violations);
            InspectCustomAttributes(field, $"{type.FullName}.{field.Name}.attributes", violations);
        }

        foreach (var property in ApiProperties(type))
        {
            InspectCoreReference(property.PropertyType, $"{type.FullName}.{property.Name}", violations);
            InspectCustomAttributes(property, $"{type.FullName}.{property.Name}.attributes", violations);
        }

        foreach (var @event in ApiEvents(type))
        {
            InspectCoreReference(@event.EventHandlerType, $"{type.FullName}.{@event.Name}", violations);
            InspectCustomAttributes(@event, $"{type.FullName}.{@event.Name}.attributes", violations);
        }

        foreach (var constructor in ApiConstructors(type))
        {
            InspectParameters(constructor.GetParameters(), $"{type.FullName}.{constructor.Name}", violations);
            InspectCustomAttributes(constructor, $"{type.FullName}.{constructor.Name}.attributes", violations);
        }

        foreach (var method in ApiMethods(type))
        {
            InspectCoreReference(method.ReturnType, $"{type.FullName}.{method.Name}.return", violations);
            InspectParameters(method.GetParameters(), $"{type.FullName}.{method.Name}", violations);
            InspectCustomAttributes(method, $"{type.FullName}.{method.Name}.attributes", violations);

            foreach (var genericArgument in method.GetGenericArguments())
            {
                InspectCoreReference(genericArgument, $"{type.FullName}.{method.Name}.generic-parameter", violations);
            }
        }
    }

    private static void InspectWpfSurface(Type type, List<string> violations)
    {
        InspectWpfReference(type.BaseType, $"{type.FullName}.base", violations);

        foreach (var @interface in type.GetInterfaces())
        {
            InspectWpfReference(@interface, $"{type.FullName}.interface", violations);
        }

        foreach (var field in ApiFields(type))
        {
            InspectWpfReference(field.FieldType, $"{type.FullName}.{field.Name}", violations);
        }

        foreach (var property in ApiProperties(type))
        {
            InspectWpfReference(property.PropertyType, $"{type.FullName}.{property.Name}", violations);
        }

        foreach (var @event in ApiEvents(type))
        {
            InspectWpfReference(@event.EventHandlerType, $"{type.FullName}.{@event.Name}", violations);
        }

        foreach (var constructor in ApiConstructors(type))
        {
            InspectWpfParameters(constructor.GetParameters(), $"{type.FullName}.{constructor.Name}", violations);
        }

        foreach (var method in ApiMethods(type))
        {
            InspectWpfReference(method.ReturnType, $"{type.FullName}.{method.Name}.return", violations);
            InspectWpfParameters(method.GetParameters(), $"{type.FullName}.{method.Name}", violations);
        }
    }

    private static void InspectParameters(
        IEnumerable<ParameterInfo> parameters,
        string owner,
        List<string> violations)
    {
        foreach (var parameter in parameters)
        {
            InspectCoreReference(parameter.ParameterType, $"{owner}({parameter.Name})", violations);
            InspectCustomAttributes(parameter, $"{owner}({parameter.Name}).attributes", violations);
        }
    }

    private static void InspectWpfParameters(
        IEnumerable<ParameterInfo> parameters,
        string owner,
        List<string> violations)
    {
        foreach (var parameter in parameters)
        {
            InspectWpfReference(parameter.ParameterType, $"{owner}({parameter.Name})", violations);
        }
    }

    private static void InspectCustomAttributes(
        MemberInfo member,
        string owner,
        List<string> violations)
    {
        foreach (var attribute in CustomAttributeData.GetCustomAttributes(member))
        {
            InspectCoreReference(attribute.AttributeType, owner, violations);
            InspectParameters(attribute.Constructor.GetParameters(), owner, violations);
        }
    }

    private static void InspectCustomAttributes(
        ParameterInfo parameter,
        string owner,
        List<string> violations)
    {
        foreach (var attribute in CustomAttributeData.GetCustomAttributes(parameter))
        {
            InspectCoreReference(attribute.AttributeType, owner, violations);
            InspectParameters(attribute.Constructor.GetParameters(), owner, violations);
        }
    }

    private static void InspectCoreReference(
        Type? referencedType,
        string owner,
        List<string> violations)
    {
        InspectCoreReference(referencedType, owner, violations, []);
    }

    private static void InspectCoreReference(
        Type? referencedType,
        string owner,
        List<string> violations,
        HashSet<Type> visited)
    {
        if (referencedType is null || !visited.Add(referencedType))
        {
            return;
        }

        if (referencedType.IsGenericParameter)
        {
            foreach (var constraint in referencedType.GetGenericParameterConstraints())
            {
                InspectCoreReference(constraint, owner, violations, visited);
            }

            return;
        }

        var nullableUnderlyingType = Nullable.GetUnderlyingType(referencedType);
        if (nullableUnderlyingType is not null)
        {
            InspectCoreReference(nullableUnderlyingType, owner, violations, visited);
            return;
        }

        if (referencedType.HasElementType)
        {
            InspectCoreReference(referencedType.GetElementType(), owner, violations, visited);
            return;
        }

        var typeToCheck = referencedType.IsGenericType
            ? referencedType.GetGenericTypeDefinition()
            : referencedType;

        if (IsDobbyCoreType(typeToCheck) &&
            !AllowedCoreTypes.Contains(typeToCheck.FullName ?? typeToCheck.Name))
        {
            violations.Add($"{owner}: references {DisplayName(typeToCheck)} which is outside the ADR-0001 allowed surface.");
        }

        if (referencedType.IsGenericType)
        {
            foreach (var genericArgument in referencedType.GetGenericArguments())
            {
                InspectCoreReference(genericArgument, owner, violations, visited);
            }
        }
    }

    private static void InspectWpfReference(
        Type? referencedType,
        string owner,
        List<string> violations)
    {
        InspectWpfReference(referencedType, owner, violations, []);
    }

    private static void InspectWpfReference(
        Type? referencedType,
        string owner,
        List<string> violations,
        HashSet<Type> visited)
    {
        if (referencedType is null || !visited.Add(referencedType))
        {
            return;
        }

        if (referencedType.IsGenericParameter)
        {
            foreach (var constraint in referencedType.GetGenericParameterConstraints())
            {
                InspectWpfReference(constraint, owner, violations, visited);
            }

            return;
        }

        var nullableUnderlyingType = Nullable.GetUnderlyingType(referencedType);
        if (nullableUnderlyingType is not null)
        {
            InspectWpfReference(nullableUnderlyingType, owner, violations, visited);
            return;
        }

        if (referencedType.HasElementType)
        {
            InspectWpfReference(referencedType.GetElementType(), owner, violations, visited);
            return;
        }

        if (IsWpfType(referencedType))
        {
            violations.Add($"{owner}: references WPF/UI type {DisplayName(referencedType)}.");
        }

        if (referencedType.IsGenericType)
        {
            foreach (var genericArgument in referencedType.GetGenericArguments())
            {
                InspectWpfReference(genericArgument, owner, violations, visited);
            }
        }
    }

    private static IEnumerable<FieldInfo> ApiFields(Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly);
    }

    private static IEnumerable<PropertyInfo> ApiProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(property => IsApiMethod(property.GetMethod) || IsApiMethod(property.SetMethod));
    }

    private static IEnumerable<EventInfo> ApiEvents(Type type)
    {
        return type
            .GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(@event => IsApiMethod(@event.AddMethod) || IsApiMethod(@event.RemoveMethod));
    }

    private static IEnumerable<ConstructorInfo> ApiConstructors(Type type)
    {
        return type
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(constructor => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly);
    }

    private static IEnumerable<MethodInfo> ApiMethods(Type type)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => IsApiMethod(method) && !method.IsSpecialName);
    }

    private static bool IsApiMethod(MethodBase? method)
    {
        return method is not null &&
            (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
    }

    private static bool IsDobbyCoreType(Type type)
    {
        return type.Namespace is not null &&
            (type.Namespace.Equals("Dobby.Core", StringComparison.Ordinal) ||
                type.Namespace.StartsWith("Dobby.Core.", StringComparison.Ordinal));
    }

    private static bool IsWpfType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name;
        return WpfAssemblyNames.Contains(assemblyName ?? string.Empty) ||
            (type.Namespace is not null &&
                (type.Namespace.Equals("System.Xaml", StringComparison.Ordinal) ||
                    type.Namespace.StartsWith("System.Xaml.", StringComparison.Ordinal) ||
                    type.Namespace.Equals("System.Windows", StringComparison.Ordinal) ||
                    type.Namespace.StartsWith("System.Windows.", StringComparison.Ordinal)));
    }

    private static IEnumerable<Type> ContractsTypes()
    {
        return ContractsAssembly()
            .GetTypes()
            .Where(type => type.Namespace is not null &&
                type.Namespace.StartsWith("Dobby.Contracts", StringComparison.Ordinal) &&
                !IsCompilerGenerated(type));
    }

    private static bool IsCompilerGenerated(Type type)
    {
        return type.Name.StartsWith("<", StringComparison.Ordinal) ||
            type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;
    }

    private static string DisplayName(Type type)
    {
        return type.FullName ?? type.Name;
    }

    private static Assembly ContractsAssembly()
    {
        return typeof(IEventBus).Assembly;
    }
}
