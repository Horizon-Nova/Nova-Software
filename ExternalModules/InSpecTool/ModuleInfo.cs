// ── Module identity — read by Helix-Studio shell via MetadataLoadContext ──────
// Replaces the old Package/module.json metadata section.
// Settings schema is no longer needed here; the module manages its own UI.
using HorizonNova.HelixStudio.ModuleSdk;

[assembly: ModuleId("com.horizonnova.inspec-tool")]
[assembly: ModuleDisplayName("InSpecTool")]
[assembly: ModuleVersion("1.2.0")]
[assembly: ModuleAuthor("Horizon Nova")]
[assembly: ModuleDescription("條碼解析、欄位同步、.iwp 查找、InSpec 開檔、自動 Run 與 Input.txt 橋接。")]
[assembly: ModuleMinHostVersion("1.0.0")]
[assembly: ModuleRequiresLicense]
// Icon: embed Package/icon.png as EmbeddedResource and reference it here.
// [assembly: ModuleIconResource("InSpecTool.Package.icon.png")]
