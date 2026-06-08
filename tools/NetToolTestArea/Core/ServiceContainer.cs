using NetToolTestArea.Services;
using NetToolTestArea.Utility;

namespace NetToolTestArea.Core
{
    public sealed class ServiceContainer
    {
        public ClickModeService ClickModeService { get; }
        public ClickWindowService ClickWindowService { get; }
        public CaptureSingleService CaptureSingleService { get; }
        public CaptureWindowService CaptureWindowService { get; }
        public MemoryInjectionService MemoryInjectionService { get; }
        public FunctionAnalysisService FunctionAnalysisService { get; }
        public WindowApiService WindowApiService { get; }
        public KeyToolService KeyToolService { get; }
        public IsolatedStorageService IsolatedStorageService { get; }

        public ServiceContainer()
        {
            // 工具層
            ClickModeUtility clickModeUtility = new ClickModeUtility();
            CaptureSingleUtility captureSingleUtility = new CaptureSingleUtility(clickModeUtility);
            MemoryInjectionUtility memoryInjectionUtility = new MemoryInjectionUtility();
            WindowApiUtility windowApiUtility = new WindowApiUtility();
            KeyToolUtility keyToolUtility = new KeyToolUtility();

            // 服務層（僅負責串接）
            ClickModeService = new ClickModeService(clickModeUtility);
            ClickWindowService = new ClickWindowService(clickModeUtility);
            CaptureSingleService = new CaptureSingleService(captureSingleUtility);
            CaptureWindowService = new CaptureWindowService(clickModeUtility);
            MemoryInjectionService = new MemoryInjectionService(memoryInjectionUtility);
            FunctionAnalysisService = new FunctionAnalysisService();
            WindowApiService = new WindowApiService(windowApiUtility);
            KeyToolService = new KeyToolService(keyToolUtility);
            IsolatedStorageService = new IsolatedStorageService();
        }
    }
}


