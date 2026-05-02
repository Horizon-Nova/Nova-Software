namespace Helix_Studio.Domain.Workspace;

/// <summary>工作區名稱與描述之正規化與驗證規則。</summary>
public static class WorkspaceDomain
{
    /// <summary>正規化工作區名稱（修剪空白）。</summary>
    public static string NormalizeName(string name) => (name ?? string.Empty).Trim();

    /// <summary>正規化工作區描述（修剪空白）。</summary>
    public static string NormalizeDescription(string description) => (description ?? string.Empty).Trim();

    /// <summary>驗證工作區名稱非空白；不符時擲出例外。</summary>
    public static void EnsureNameIsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("工作區名稱不可為空白。", nameof(name));
        }
    }

    /// <summary>判斷在目前工作區總數下是否允許刪除（至少須保留一個）。</summary>
    public static bool CanDeleteWorkspace(int totalWorkspaceCount) => totalWorkspaceCount > 1;
}
