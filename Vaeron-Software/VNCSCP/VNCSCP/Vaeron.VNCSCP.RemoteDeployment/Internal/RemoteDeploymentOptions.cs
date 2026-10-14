using System.ComponentModel.DataAnnotations;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>跨機台併發上限依現場的機台規模與網路頻寬調校，放設定檔不寫死。</summary>
internal sealed class RemoteDeploymentOptions
{
    public const string SectionName = "RemoteDeployment";

    [Range(1, 64)]
    public int MaxParallelDeployments { get; init; } = 4;
}

