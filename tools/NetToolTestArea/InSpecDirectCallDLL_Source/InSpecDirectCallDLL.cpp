#define UNICODE
#define _UNICODE

#include <windows.h>
#include <stdio.h>
#include <stdarg.h>

// 說明：直接調用遠端 function 的 DLL
// 這個 DLL 會被注入到目標進程中，用於執行遠端 function 調用

// 日誌文件路徑
#define LOG_FILE_PATH L"C:\\InSpecTool\\DirectCallDebug.log"

// 寫入日誌
static void WriteLog(const wchar_t* format, ...)
{
    FILE* fp = NULL;
    _wfopen_s(&fp, LOG_FILE_PATH, L"a");
    if (fp)
    {
        va_list args;
        va_start(args, format);
        vfwprintf_s(fp, format, args);
        va_end(args);
        fwprintf_s(fp, L"\n");
        fclose(fp);
    }
}

// 結構體：CreateFileW 參數
struct CreateFileWParams
{
    LPCWSTR lpFileName;
    DWORD dwDesiredAccess;
    DWORD dwShareMode;
    LPSECURITY_ATTRIBUTES lpSecurityAttributes;
    DWORD dwCreationDisposition;
    DWORD dwFlagsAndAttributes;
    HANDLE hTemplateFile;
};

// 說明：包裝 CreateFileW 調用的函數
// 參數：functionAddress - 要調用的 function 地址
//       params - CreateFileW 參數結構體指針
extern "C" __declspec(dllexport) HANDLE WINAPI DirectCallCreateFileW(
    void* functionAddress,
    CreateFileWParams* params)
{
    if (!functionAddress || !params)
    {
        return INVALID_HANDLE_VALUE;
    }

    typedef HANDLE (WINAPI *PFN_CreateFileW)(
        LPCWSTR lpFileName,
        DWORD dwDesiredAccess,
        DWORD dwShareMode,
        LPSECURITY_ATTRIBUTES lpSecurityAttributes,
        DWORD dwCreationDisposition,
        DWORD dwFlagsAndAttributes,
        HANDLE hTemplateFile);

    PFN_CreateFileW pfn = (PFN_CreateFileW)functionAddress;
    return pfn(
        params->lpFileName,
        params->dwDesiredAccess,
        params->dwShareMode,
        params->lpSecurityAttributes,
        params->dwCreationDisposition,
        params->dwFlagsAndAttributes,
        params->hTemplateFile);
}

// 說明：直接調用任意 function（無參數）
// 參數：functionAddressPtr - 指向函數地址的指針（在遠端內存中）
extern "C" __declspec(dllexport) DWORD WINAPI DirectCallFunction(void* functionAddressPtr)
{
    WriteLog(L"[DirectCallFunction] 開始調用，參數指針: 0x%p", functionAddressPtr);
    
    if (!functionAddressPtr)
    {
        WriteLog(L"[DirectCallFunction] 錯誤：參數指針為 NULL");
        return 0;
    }

    // 從遠端內存讀取函數地址
    void* functionAddress = *(void**)functionAddressPtr;
    WriteLog(L"[DirectCallFunction] 讀取的函數地址: 0x%p", functionAddress);
    
    if (!functionAddress)
    {
        WriteLog(L"[DirectCallFunction] 錯誤：函數地址為 NULL");
        return 0;
    }

    // 嘗試多種調用約定
    DWORD result = 0;
    DWORD exceptionCode = 0;
    
    // 方法 1：嘗試 __stdcall（Windows API 標準）
    WriteLog(L"[DirectCallFunction] 嘗試使用 __stdcall 調用約定");
    __try
    {
        typedef DWORD (__stdcall *PFN_StdCall)();
        PFN_StdCall pfn = (PFN_StdCall)functionAddress;
        result = pfn();
        WriteLog(L"[DirectCallFunction] __stdcall 調用成功，返回值: 0x%08X", result);
        return result;
    }
    __except(exceptionCode = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER)
    {
        WriteLog(L"[DirectCallFunction] __stdcall 調用失敗，異常代碼: 0x%08X", exceptionCode);
    }
    
    // 方法 2：嘗試 __cdecl（C 標準）
    WriteLog(L"[DirectCallFunction] 嘗試使用 __cdecl 調用約定");
    __try
    {
        typedef DWORD (__cdecl *PFN_Cdecl)();
        PFN_Cdecl pfn = (PFN_Cdecl)functionAddress;
        result = pfn();
        WriteLog(L"[DirectCallFunction] __cdecl 調用成功，返回值: 0x%08X", result);
        return result;
    }
    __except(exceptionCode = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER)
    {
        WriteLog(L"[DirectCallFunction] __cdecl 調用失敗，異常代碼: 0x%08X", exceptionCode);
        return exceptionCode;
    }
}


// 說明：DLL 入口點
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

