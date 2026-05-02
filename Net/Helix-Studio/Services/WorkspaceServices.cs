using System;
using System.Collections.Generic;
using System.Linq;
using Helix_Studio.Domain.Workspace;
using Helix_Studio.Infrastructure.Persistence;
using Helix_Studio.Model.Workspace;

namespace Helix_Studio.Services;

/// <summary>工作區的 CRUD、啟用切換與刪除規則，並於切換時寫入事件日誌。</summary>
public class WorkspaceServices
{
    private readonly WorkspaceRepository _repository;

    /// <summary>以工作區儲存庫建立服務。</summary>
    public WorkspaceServices(WorkspaceRepository repository)
    {
        _repository = repository;
    }

    /// <summary>當作用中工作區變更完成後觸發，供掃描模組目錄等副作用註冊。</summary>
    public event Action? WorkspaceActivated;

    /// <summary>初始化儲存層的預設資料與結構（若實作需要）。</summary>
    public void Initialize()
        => _repository.Initialize();

    /// <summary>取得所有工作區列表。</summary>
    public List<WorkspaceModel> GetAll()
        => _repository.GetAll();

    /// <summary>取得目前標記為啟用中的工作區。</summary>
    public WorkspaceModel? GetActive()
        => _repository.GetActive();

    /// <summary>以經正規化與驗證的名稱與說明建立新工作區。</summary>
    public WorkspaceModel Create(string name, string description = "")
    {
        var normalizedName = WorkspaceDomain.NormalizeName(name);
        var normalizedDescription = WorkspaceDomain.NormalizeDescription(description);
        WorkspaceDomain.EnsureNameIsValid(normalizedName);
        return _repository.Create(normalizedName, normalizedDescription);
    }

    /// <summary>更新指定工作區名稱與描述（經領域規則正規化）。</summary>
    public void Update(int id, string name, string description)
    {
        var normalizedName = WorkspaceDomain.NormalizeName(name);
        var normalizedDescription = WorkspaceDomain.NormalizeDescription(description);
        WorkspaceDomain.EnsureNameIsValid(normalizedName);
        _repository.Update(id, normalizedName, normalizedDescription);
    }

    /// <summary>刪除工作區；依數量規則可能拒絕刪除，刪除使用中項目時會改選另一工作區。</summary>
    public bool Delete(int id)
    {
        var count = _repository.QueryWorkspaceCount();
        if (!WorkspaceDomain.CanDeleteWorkspace(count)) return false;

        var active = _repository.GetActive();
        var deleted = _repository.Delete(id);
        if (!deleted) return false;

        if (active?.Id == id)
        {
            var fallback = _repository.GetAll().FirstOrDefault();
            if (fallback is not null)
            {
                _repository.SetActive(fallback.Id);
            }
        }

        return true;
    }

    /// <summary>設定指定工作區為啟用、寫入日誌並觸發 <see cref="WorkspaceActivated"/>。</summary>
    public void SetActive(int id)
    {
        _repository.SetActive(id);
        var name = _repository.GetActive()?.Name ?? id.ToString();
        EventLogServices.Info($"已切換工作區：{name}", "Workspace");
        WorkspaceActivated?.Invoke();
    }
}

