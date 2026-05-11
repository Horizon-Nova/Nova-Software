using System;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// 概覽頁面的零邏輯轉發服務。
/// </summary>
public sealed class OverviewServices(OverviewDomain domain)
{
    /// <summary>
    /// 概覽頁面資料變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    /// <summary>
    /// 查詢概覽頁面資料快照。
    /// </summary>
    public OverviewModel QueryOverview() => domain.QueryOverview();
}

