// ── Module identity (read by HN-Studio shell via assembly attributes) ──────
// These attributes replace the old module.json approach.
// The shell uses MetadataLoadContext to read them without executing module code.
using HorizonNova.HelixStudio.ModuleSdk;

[assembly: ModuleId("com.horizonnova.sample")]
[assembly: ModuleDisplayName("Sample Module")]
[assembly: ModuleVersion("1.0.0")]
[assembly: ModuleAuthor("Horizon Nova")]
[assembly: ModuleDescription("示範模組：展示如何實作 IModule 並整合至 HN-Studio 殼層。")]
[assembly: ModuleMinHostVersion("1.0.0")]
[assembly: ModuleIconResource("HorizonNova.SampleModule.Assets.icon.png")]

