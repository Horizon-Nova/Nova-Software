using System;
using Helix_Studio.Model;

namespace Helix_Studio.Core;

/// <summary>
/// 模組健康狀態事件匯流排。
/// </summary>
public sealed class ModuleHealthSink
{
    /// <summary>
    /// 當模組健康狀態變更時觸發。
    /// </summary>
    public event Action<string, ModuleHealth, string?>? Changed;

    /// <summary>
    /// 發佈模組健康狀態變更事件。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    /// <param name="health">健康狀態。</param>
    /// <param name="message">說明訊息。</param>
    public void Publish(string moduleId, ModuleHealth health, string? message)
        => Changed?.Invoke(moduleId, health, message);
}

