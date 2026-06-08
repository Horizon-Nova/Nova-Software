using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Infrastructure.Modules;

internal static class ModuleMetadataReader
{
    private const string ModuleInterfaceFullName = "HorizonNova.HelixStudio.ModuleSdk.IModule";
    private const string ModuleWithSettingsInterfaceFullName = "HorizonNova.HelixStudio.ModuleSdk.IModuleWithSettings";

    public static ModuleMetadataSnapshot ReadFromAssembly(string dllPath, string moduleDirectoryPath)
    {
        var assemblyName = AssemblyName.GetAssemblyName(dllPath);
        var resolverPaths = QueryResolverPaths(dllPath);
        var resolver = new PathAssemblyResolver(resolverPaths);
        using var metadataLoadContext = new MetadataLoadContext(resolver);
        var assembly = metadataLoadContext.LoadFromAssemblyPath(dllPath);
        var moduleType = QueryModuleType(assembly);
        var moduleId = QueryAssemblyAttributeValue<ModuleIdAttribute>(assembly, assemblyName.Name ?? Path.GetFileNameWithoutExtension(dllPath));
        var version = QueryAssemblyAttributeValue<ModuleVersionAttribute>(assembly, assemblyName.Version?.ToString() ?? "0.0.0");

        return new ModuleMetadataSnapshot(
            moduleId,
            QueryAssemblyAttributeValue<ModuleDisplayNameAttribute>(assembly, moduleId),
            version,
            QueryAssemblyAttributeValue<ModuleAuthorAttribute>(assembly, string.Empty),
            QueryAssemblyAttributeValue<ModuleDescriptionAttribute>(assembly, string.Empty),
            QueryAssemblyAttributeValue<ModuleMinHostVersionAttribute>(assembly, string.Empty),
            moduleDirectoryPath,
            dllPath,
            QueryAssemblyAttributeValue<ModuleRequiresLicenseAttribute>(assembly, defaultValue: false),
            moduleType is not null,
            moduleType is not null && ImplementsInterface(moduleType, ModuleWithSettingsInterfaceFullName));
    }

    private static IReadOnlyList<string> QueryResolverPaths(string dllPath)
    {
        var runtimeDirectoryPath = RuntimeEnvironment.GetRuntimeDirectory();
        var moduleSdkPath = typeof(IModule).Assembly.Location;
        var paths = Directory.EnumerateFiles(runtimeDirectoryPath, "*.dll")
            .Concat([dllPath, moduleSdkPath])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return paths;
    }

    private static Type? QueryModuleType(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .FirstOrDefault(static type => !type.IsAbstract && ImplementsInterface(type, ModuleInterfaceFullName));
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(static type => type is not null)
                .Cast<Type>()
                .FirstOrDefault(static type => !type.IsAbstract && ImplementsInterface(type, ModuleInterfaceFullName));
        }
    }

    private static bool ImplementsInterface(Type type, string interfaceFullName)
    {
        if (string.Equals(type.FullName, interfaceFullName, StringComparison.Ordinal))
        {
            return true;
        }

        return type.GetInterfaces().Any(candidate => string.Equals(candidate.FullName, interfaceFullName, StringComparison.Ordinal));
    }

    private static string QueryAssemblyAttributeValue<TAttribute>(Assembly assembly, string fallback)
        where TAttribute : Attribute
    {
        var attribute = assembly.GetCustomAttributesData()
            .FirstOrDefault(attribute => string.Equals(attribute.AttributeType.FullName, typeof(TAttribute).FullName, StringComparison.Ordinal));
        if (attribute is null || attribute.ConstructorArguments.Count == 0)
        {
            return fallback;
        }

        return attribute.ConstructorArguments[0].Value as string ?? fallback;
    }

    private static bool QueryAssemblyAttributeValue<TAttribute>(Assembly assembly, bool defaultValue)
        where TAttribute : Attribute
        => assembly.GetCustomAttributesData()
            .Any(attribute => string.Equals(attribute.AttributeType.FullName, typeof(TAttribute).FullName, StringComparison.Ordinal))
            ? true
            : defaultValue;
}

internal sealed record ModuleMetadataSnapshot(
    string Id,
    string DisplayName,
    string Version,
    string Author,
    string Description,
    string MinHostVersion,
    string FolderPath,
    string DllPath,
    bool RequiresLicense,
    bool IsLoadable,
    bool HasSettings);
