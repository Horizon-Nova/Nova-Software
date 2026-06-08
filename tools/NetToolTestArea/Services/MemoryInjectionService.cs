using System;
using NetToolTestArea.Utility;

namespace NetToolTestArea.Services
{
    public class MemoryInjectionService
    {
        private MemoryInjectionUtility memoryInjectionUtility;

        public MemoryInjectionService(MemoryInjectionUtility memoryInjectionUtility)
        {
            this.memoryInjectionUtility = memoryInjectionUtility;
        }

        public int FindInSpecProcessId() => memoryInjectionUtility.FindInSpecProcessId();

        public bool TryGetInSpecTarget(out int processId, out string windowTitle, out string processPath) => 
            memoryInjectionUtility.TryGetInSpecTarget(out processId, out windowTitle, out processPath);

        public string QueryDefaultHookDllPath() => memoryInjectionUtility.QueryDefaultHookDllPath();

        public bool TryEnsureInSpecDllInjected(string dllPath, out int processId, out string errorMessage) => 
            memoryInjectionUtility.TryEnsureInSpecDllInjected(dllPath, out processId, out errorMessage);

        public bool InjectDLL(int processId, string dllPath) => memoryInjectionUtility.InjectDLL(processId, dllPath);

        public bool TryInjectDllToProcess(int processId, string dllPath, Action<string> logCallback, out string versionDisplay, out string errorMessage)
        {
            var result = memoryInjectionUtility.TryInjectDllToProcess(processId, dllPath, logCallback);
            versionDisplay = result.VersionDisplay;
            errorMessage = result.ErrorMessage; return result.Success;
        }

        public bool TryTestInjectToNotepad(string dllPath, Action<string> logCallback, out int notepadProcessId, out string errorMessage)
        {
            var result = memoryInjectionUtility.TryTestInjectToNotepad(dllPath, logCallback);
            notepadProcessId = result.ProcessId;
            errorMessage = result.ErrorMessage; return result.Success;
        }

        public bool IsInjected => memoryInjectionUtility.IsInjected;
        public int TargetProcessId => memoryInjectionUtility.TargetProcessId;

        public void SetInjected(int processId, string dllPath) => memoryInjectionUtility.SetInjected(processId, dllPath);

        public bool ExecuteScript(string script) => memoryInjectionUtility.ExecuteScript(script);

        public bool CallFunctionByAddress(int processId, IntPtr functionAddress, IntPtr parameter) => memoryInjectionUtility.CallFunctionByAddress(processId, functionAddress, parameter);

        public int TestDllInjection(string dllPath) => memoryInjectionUtility.TestDllInjection(dllPath);
    }
}

