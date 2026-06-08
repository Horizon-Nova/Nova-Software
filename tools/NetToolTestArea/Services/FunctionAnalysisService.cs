using System;
using NetToolTestArea.Core;
using NetToolTestArea.Utility;

namespace NetToolTestArea.Services
{
    public class FunctionAnalysisService
    {
        private Action<string> logCallback;

        public FunctionAnalysisService() { }

        public FunctionAnalysisUtility.ScanResult ScanInSpecProcessFunctions() => FunctionAnalysisUtility.ScanInSpecProcessFunctionsForDisplay();

        public void SetLogCallback(Action<string> callback) => logCallback = callback;

        public bool StartRecording(bool enableApiMonitor, string dllPath) => FunctionAnalysisUtility.StartRecording(enableApiMonitor, dllPath, logCallback);

        public FunctionAnalysisUtility.RecordingResult StopRecording(bool enableApiMonitor) => FunctionAnalysisUtility.StopRecording(enableApiMonitor, logCallback);

        public bool IsRecording => FunctionAnalysisUtility.IsRecording;

        public bool TriggerInSpecRun(out string errorMessage) => WmCommandCore.TriggerInSpecRun(out errorMessage);
    }
}

