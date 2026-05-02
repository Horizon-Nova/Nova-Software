using System.Collections.ObjectModel;
using System.Windows;

namespace Helix_Studio.Infrastructure.Extensions;

/// <summary><see cref="ResourceDictionary"/> 集合之擴充方法。</summary>
public static class ResourceDictionaryExtensions
{
    /// <summary>移除來源 URI 包含指定片段之資源字典。</summary>
    public static void RemoveBySourceMatch(this Collection<ResourceDictionary> dictionaries, string pathFragment)
    {
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.ToString();
            if (source?.Contains(pathFragment, StringComparison.OrdinalIgnoreCase) == true)
            {
                dictionaries.RemoveAt(i);
            }
        }
    }
}
