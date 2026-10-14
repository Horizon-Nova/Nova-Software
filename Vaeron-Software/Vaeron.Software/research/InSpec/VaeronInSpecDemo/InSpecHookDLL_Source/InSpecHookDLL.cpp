#define UNICODE
#define _UNICODE

#include <windows.h>
#include <psapi.h>
#include <stdint.h>
#include <string>
#include <vector>
#include <intrin.h>
#include <dbghelp.h>
#include <cstdio>
#include <commctrl.h>
#pragma comment(lib, "dbghelp.lib")

#define MAX_LOG_QUEUE_SIZE 1000
#define LOG_PIPE_NAME L"\\\\.\\pipe\\InSpecHookDLL_LogPipe"

struct IatPatch
{
    void** iatEntry;
    void* original;
};

static bool g_isApiHookEnabled = false;
static HMODULE g_hSelfModule = NULL;

static CRITICAL_SECTION g_csLog;
static HANDLE g_hLogEvent = NULL;
static HANDLE g_hLogThread = NULL;
static bool g_isLogThreadRunning = false;

static wchar_t* g_logQueue[MAX_LOG_QUEUE_SIZE];
static int g_logQueueHead = 0;
static int g_logQueueTail = 0;
static int g_logQueueCount = 0;
static HANDLE g_hLogPipe = INVALID_HANDLE_VALUE;

static wchar_t g_lastLogMessage[512] = {0};
static int g_repeatCount = 0;

static std::vector<IatPatch> g_iatPatches;

// kernel32.dll 函數類型定義
typedef HANDLE (WINAPI *PFN_CreateFileW)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
typedef HANDLE (WINAPI *PFN_CreateFileA)(LPCSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
typedef BOOL (WINAPI *PFN_ReadFile)(HANDLE, LPVOID, DWORD, LPDWORD, LPOVERLAPPED);
typedef BOOL (WINAPI *PFN_WriteFile)(HANDLE, LPCVOID, DWORD, LPDWORD, LPOVERLAPPED);
typedef BOOL (WINAPI *PFN_DeleteFileW)(LPCWSTR);
typedef BOOL (WINAPI *PFN_DeleteFileA)(LPCSTR);
typedef BOOL (WINAPI *PFN_MoveFileW)(LPCWSTR, LPCWSTR);
typedef BOOL (WINAPI *PFN_MoveFileA)(LPCSTR, LPCSTR);
typedef BOOL (WINAPI *PFN_CopyFileW)(LPCWSTR, LPCWSTR, BOOL);
typedef BOOL (WINAPI *PFN_CopyFileA)(LPCSTR, LPCSTR, BOOL);
typedef HMODULE (WINAPI *PFN_LoadLibraryW)(LPCWSTR);
typedef HMODULE (WINAPI *PFN_LoadLibraryA)(LPCSTR);
typedef FARPROC (WINAPI *PFN_GetProcAddress)(HMODULE, LPCSTR);
typedef LPVOID (WINAPI *PFN_VirtualAlloc)(LPVOID, SIZE_T, DWORD, DWORD);
typedef BOOL (WINAPI *PFN_VirtualFree)(LPVOID, SIZE_T, DWORD);

// user32.dll 函數類型定義
typedef LRESULT (WINAPI *PFN_SendMessageW)(HWND, UINT, WPARAM, LPARAM);
typedef LRESULT (WINAPI *PFN_SendMessageA)(HWND, UINT, WPARAM, LPARAM);
typedef BOOL (WINAPI *PFN_PostMessageW)(HWND, UINT, WPARAM, LPARAM);
typedef BOOL (WINAPI *PFN_PostMessageA)(HWND, UINT, WPARAM, LPARAM);
typedef HWND (WINAPI *PFN_FindWindowW)(LPCWSTR, LPCWSTR);
typedef HWND (WINAPI *PFN_FindWindowA)(LPCSTR, LPCSTR);
typedef int (WINAPI *PFN_GetWindowTextW)(HWND, LPWSTR, int);
typedef int (WINAPI *PFN_GetWindowTextA)(HWND, LPSTR, int);
typedef BOOL (WINAPI *PFN_SetWindowTextW)(HWND, LPCWSTR);
typedef BOOL (WINAPI *PFN_SetWindowTextA)(HWND, LPCSTR);

// kernel32.dll 原始函數指針
static PFN_CreateFileW g_pfnOriginalCreateFileW = NULL;
static PFN_CreateFileA g_pfnOriginalCreateFileA = NULL;
static PFN_ReadFile g_pfnOriginalReadFile = NULL;
static PFN_WriteFile g_pfnOriginalWriteFile = NULL;
static PFN_DeleteFileW g_pfnOriginalDeleteFileW = NULL;
static PFN_DeleteFileA g_pfnOriginalDeleteFileA = NULL;
static PFN_MoveFileW g_pfnOriginalMoveFileW = NULL;
static PFN_MoveFileA g_pfnOriginalMoveFileA = NULL;
static PFN_CopyFileW g_pfnOriginalCopyFileW = NULL;
static PFN_CopyFileA g_pfnOriginalCopyFileA = NULL;
static PFN_LoadLibraryW g_pfnOriginalLoadLibraryW = NULL;
static PFN_LoadLibraryA g_pfnOriginalLoadLibraryA = NULL;
static PFN_GetProcAddress g_pfnOriginalGetProcAddress = NULL;
static PFN_VirtualAlloc g_pfnOriginalVirtualAlloc = NULL;
static PFN_VirtualFree g_pfnOriginalVirtualFree = NULL;

// user32.dll 原始函數指針
static PFN_SendMessageW g_pfnOriginalSendMessageW = NULL;
static PFN_SendMessageA g_pfnOriginalSendMessageA = NULL;
static PFN_PostMessageW g_pfnOriginalPostMessageW = NULL;
static PFN_PostMessageA g_pfnOriginalPostMessageA = NULL;
static PFN_FindWindowW g_pfnOriginalFindWindowW = NULL;
static PFN_FindWindowA g_pfnOriginalFindWindowA = NULL;
static PFN_GetWindowTextW g_pfnOriginalGetWindowTextW = NULL;
static PFN_GetWindowTextA g_pfnOriginalGetWindowTextA = NULL;
static PFN_SetWindowTextW g_pfnOriginalSetWindowTextW = NULL;
static PFN_SetWindowTextA g_pfnOriginalSetWindowTextA = NULL;

static void QueueLogMessage(const wchar_t* message);
static void WriteApiLog(const wchar_t* message);
static void WriteApiLogWithDeduplication(const wchar_t* message);
static DWORD WINAPI LogThreadProc(LPVOID lpParam);
static void GetHwndInfo(HWND hWnd, wchar_t* className, int classNameSize, wchar_t* windowText, int windowTextSize);

static void* GetProcAddressByName(HMODULE module, const char* name)
{
    return (void*)GetProcAddress(module, name);
}

static bool IsWideStringNullOrEmpty(const wchar_t* s)
{
    return s == NULL || s[0] == L'\0';
}

static void QueueLogMessage(const wchar_t* message)
{
    if (IsWideStringNullOrEmpty(message)) return;

    size_t len = wcslen(message);
    size_t bytes = (len + 1) * sizeof(wchar_t);

    wchar_t* copy = (wchar_t*)malloc(bytes);
    if (!copy) return;
    memcpy(copy, message, bytes);

    EnterCriticalSection(&g_csLog);

    if (g_logQueueCount >= MAX_LOG_QUEUE_SIZE)
    {
        free(copy);
        LeaveCriticalSection(&g_csLog);
        return;
    }

    g_logQueue[g_logQueueTail] = copy;
    g_logQueueTail = (g_logQueueTail + 1) % MAX_LOG_QUEUE_SIZE;
    g_logQueueCount++;

    LeaveCriticalSection(&g_csLog);

    if (g_hLogEvent)
    {
        SetEvent(g_hLogEvent);
    }
}

static void WriteApiLog(const wchar_t* message)
{
    if (!g_isLogThreadRunning) return;
    QueueLogMessage(message);
}

static void WriteApiLogWithDeduplication(const wchar_t* message)
{
    if (!g_isApiHookEnabled || !g_isLogThreadRunning) return;

    if (wcscmp(message, g_lastLogMessage) == 0)
    {
        g_repeatCount++;
        SYSTEMTIME st;
        GetLocalTime(&st);
        wchar_t updateMarker[64];
        swprintf_s(updateMarker, 64, L"[UPDATE_TIME]%02d:%02d:%02d.%03d", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
        WriteApiLog(updateMarker);
        return;
    }

    wcscpy_s(g_lastLogMessage, 512, message);
    g_repeatCount = 0;
    WriteApiLog(message);
}

static DWORD WINAPI LogThreadProc(LPVOID lpParam)
{
    (void)lpParam;

    while (g_isLogThreadRunning)
    {
        WaitForSingleObject(g_hLogEvent, INFINITE);

        if (!g_isLogThreadRunning)
        {
            break;
        }

        while (true)
        {
            wchar_t* logMsg = NULL;

            EnterCriticalSection(&g_csLog);
            if (g_logQueueCount > 0)
            {
                logMsg = g_logQueue[g_logQueueHead];
                g_logQueueHead = (g_logQueueHead + 1) % MAX_LOG_QUEUE_SIZE;
                g_logQueueCount--;
            }
            LeaveCriticalSection(&g_csLog);

            if (!logMsg)
            {
                break;
            }

            if (g_hLogPipe == INVALID_HANDLE_VALUE)
            {
                g_hLogPipe = CreateFileW(LOG_PIPE_NAME, GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);
                if (g_hLogPipe == INVALID_HANDLE_VALUE)
                {
                    free(logMsg);
                    Sleep(50);
                    continue;
                }
            }

            SYSTEMTIME st;
            GetLocalTime(&st);

            wchar_t logEntry[1024];
            swprintf_s(logEntry, 1024, L"[%02d:%02d:%02d.%03d] %s",
                st.wHour, st.wMinute, st.wSecond, st.wMilliseconds,
                logMsg);

            DWORD written = 0;
            if (!WriteFile(g_hLogPipe, logEntry, (DWORD)(wcslen(logEntry) * sizeof(wchar_t)), &written, NULL))
            {
                CloseHandle(g_hLogPipe);
                g_hLogPipe = INVALID_HANDLE_VALUE;
            }
            else
            {
                FlushFileBuffers(g_hLogPipe);
            }

            free(logMsg);
        }
    }

    return 0;
}

static void GetHwndInfo(HWND hWnd, wchar_t* className, int classNameSize, wchar_t* windowText, int windowTextSize)
{
    if (className && classNameSize > 0)
    {
        className[0] = L'\0';
    }
    if (windowText && windowTextSize > 0)
    {
        windowText[0] = L'\0';
    }

    if (!hWnd)
    {
        return;
    }

    if (className && classNameSize > 0)
    {
        GetClassNameW(hWnd, className, classNameSize);
    }

    if (windowText && windowTextSize > 0)
    {
        GetWindowTextW(hWnd, windowText, windowTextSize);
    }
}

static void GetCallerInfo(wchar_t* moduleName, int moduleNameSize, void** callerAddress)
{
    if (moduleName && moduleNameSize > 0)
    {
        moduleName[0] = L'\0';
    }
    if (callerAddress)
    {
        *callerAddress = NULL;
    }

    void* frames[16] = {0};
    USHORT frameCount = CaptureStackBackTrace(2, 16, frames, NULL);
    
    if (frameCount < 2)
    {
        return;
    }

    void* realCaller = frames[0];
    if (!realCaller)
    {
        return;
    }

    HMODULE hModule = NULL;
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCWSTR)realCaller, &hModule))
    {
        wchar_t modulePath[MAX_PATH] = {0};
        if (GetModuleFileNameW(hModule, modulePath, MAX_PATH))
        {
            const wchar_t* fileName = wcsrchr(modulePath, L'\\');
            if (fileName)
            {
                fileName++;
            }
            else
            {
                fileName = modulePath;
            }
            
            wchar_t moduleNameLower[MAX_PATH] = {0};
            wcscpy_s(moduleNameLower, MAX_PATH, fileName);
            for (int i = 0; moduleNameLower[i] != L'\0'; i++)
            {
                if (moduleNameLower[i] >= L'A' && moduleNameLower[i] <= L'Z')
                {
                    moduleNameLower[i] = (wchar_t)(moduleNameLower[i] - L'A' + L'a');
                }
            }
            
            if (wcscmp(moduleNameLower, L"inspechookdll.dll") == 0)
            {
                if (frameCount >= 2)
                {
                    realCaller = frames[1];
                    if (realCaller)
                    {
                        hModule = NULL;
                        if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                            (LPCWSTR)realCaller, &hModule))
                        {
                            if (GetModuleFileNameW(hModule, modulePath, MAX_PATH))
                            {
                                const wchar_t* fileName2 = wcsrchr(modulePath, L'\\');
                                if (fileName2)
                                {
                                    fileName2++;
                                }
                                else
                                {
                                    fileName2 = modulePath;
                                }
                                if (moduleName && moduleNameSize > 0)
                                {
                                    wcsncpy_s(moduleName, moduleNameSize, fileName2, _TRUNCATE);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (moduleName && moduleNameSize > 0)
                {
                    wcsncpy_s(moduleName, moduleNameSize, fileName, _TRUNCATE);
                }
            }
        }

        if (callerAddress && realCaller)
        {
            uintptr_t moduleBase = (uintptr_t)hModule;
            uintptr_t addressOffset = (uintptr_t)realCaller - moduleBase;
            *callerAddress = (void*)addressOffset;
        }
    }
}

static bool PatchIatForOneFunction(
    HMODULE module,
    const char* importedDllNameLower,
    const char* functionName,
    void* hookFunction,
    void** originalFunctionOut)
{
    if (!module || !importedDllNameLower || !functionName || !hookFunction)
    {
        return false;
    }

    uint8_t* base = (uint8_t*)module;
    IMAGE_DOS_HEADER* dos = (IMAGE_DOS_HEADER*)base;
    if (dos->e_magic != IMAGE_DOS_SIGNATURE)
    {
        return false;
    }

    IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE)
    {
        return false;
    }

    IMAGE_DATA_DIRECTORY importDir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDir.VirtualAddress == 0 || importDir.Size == 0)
    {
        return false;
    }

    IMAGE_IMPORT_DESCRIPTOR* desc = (IMAGE_IMPORT_DESCRIPTOR*)(base + importDir.VirtualAddress);
    for (; desc->Name != 0; desc++)
    {
        const char* dllName = (const char*)(base + desc->Name);
        if (!dllName) continue;

        char dllLower[64] = {0};
        size_t dllLen = strlen(dllName);
        size_t copyLen = dllLen < (sizeof(dllLower) - 1) ? dllLen : (sizeof(dllLower) - 1);
        for (size_t i = 0; i < copyLen; i++)
        {
            char c = dllName[i];
            if (c >= 'A' && c <= 'Z')
            {
                c = (char)(c - 'A' + 'a');
            }
            dllLower[i] = c;
        }
        dllLower[copyLen] = '\0';

        if (strcmp(dllLower, importedDllNameLower) != 0)
        {
            continue;
        }

        IMAGE_THUNK_DATA* firstThunk = (IMAGE_THUNK_DATA*)(base + desc->FirstThunk);
        IMAGE_THUNK_DATA* origThunk = desc->OriginalFirstThunk
            ? (IMAGE_THUNK_DATA*)(base + desc->OriginalFirstThunk)
            : firstThunk;

        for (; origThunk->u1.AddressOfData != 0; origThunk++, firstThunk++)
        {
            if (IMAGE_SNAP_BY_ORDINAL(origThunk->u1.Ordinal))
            {
                continue;
            }

            IMAGE_IMPORT_BY_NAME* importByName = (IMAGE_IMPORT_BY_NAME*)(base + origThunk->u1.AddressOfData);
            const char* importedName = (const char*)importByName->Name;
            if (!importedName) continue;

            if (strcmp(importedName, functionName) != 0)
            {
                continue;
            }

            void** iatEntry = (void**)&firstThunk->u1.Function;
            void* original = *iatEntry;

            if (originalFunctionOut && *originalFunctionOut == NULL)
            {
                *originalFunctionOut = original;
            }

            DWORD oldProtect = 0;
            if (!VirtualProtect(iatEntry, sizeof(void*), PAGE_READWRITE, &oldProtect))
            {
                return false;
            }

            *iatEntry = hookFunction;
            VirtualProtect(iatEntry, sizeof(void*), oldProtect, &oldProtect);
            FlushInstructionCache(GetCurrentProcess(), iatEntry, sizeof(void*));

            g_iatPatches.push_back({ iatEntry, original });
            return true;
        }
    }

    return false;
}

static void RestoreAllIatPatches()
{
    for (const auto& patch : g_iatPatches)
    {
        if (!patch.iatEntry) continue;
        DWORD oldProtect = 0;
        if (VirtualProtect(patch.iatEntry, sizeof(void*), PAGE_READWRITE, &oldProtect))
        {
            *(patch.iatEntry) = patch.original;
            VirtualProtect(patch.iatEntry, sizeof(void*), oldProtect, &oldProtect);
            FlushInstructionCache(GetCurrentProcess(), patch.iatEntry, sizeof(void*));
        }
    }

    g_iatPatches.clear();
}

static void InstallIatHooksForAllModules()
{
    HMODULE modules[1024] = {0};
    DWORD needed = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules, sizeof(modules), &needed))
    {
        return;
    }

    DWORD moduleCount = needed / sizeof(HMODULE);
    struct HookTarget
    {
        const char* importedDllLower;
        const char* funcName;
        void* hookFunc;
        void** originalOut;
    };

    // 此函數已不再使用，保留為空函數以避免編譯錯誤
    (void)modules;
    (void)needed;
    (void)moduleCount;
}


static HANDLE WINAPI Hooked_CreateFileW(
    LPCWSTR lpFileName,
    DWORD dwDesiredAccess,
    DWORD dwShareMode,
    LPSECURITY_ATTRIBUTES lpSecurityAttributes,
    DWORD dwCreationDisposition,
    DWORD dwFlagsAndAttributes,
    HANDLE hTemplateFile)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    wchar_t logMsg[1024];
    if (lpFileName)
    {
        if (callerAddress && moduleName[0] != L'\0')
        {
            swprintf_s(logMsg, 1024, L"[API] CreateFileW(fileName=\"%s\", access=0x%08X, creation=0x%08X) <- %s+0x%p",
                lpFileName, dwDesiredAccess, dwCreationDisposition, moduleName, callerAddress);
        }
        else
        {
            swprintf_s(logMsg, 1024, L"[API] CreateFileW(fileName=\"%s\", access=0x%08X, creation=0x%08X)",
                lpFileName, dwDesiredAccess, dwCreationDisposition);
        }
    }
    else
    {
        if (callerAddress && moduleName[0] != L'\0')
        {
            swprintf_s(logMsg, 1024, L"[API] CreateFileW(fileName=NULL, access=0x%08X, creation=0x%08X) <- %s+0x%p",
                dwDesiredAccess, dwCreationDisposition, moduleName, callerAddress);
        }
        else
        {
            swprintf_s(logMsg, 1024, L"[API] CreateFileW(fileName=NULL, access=0x%08X, creation=0x%08X)",
                dwDesiredAccess, dwCreationDisposition);
        }
    }
    WriteApiLogWithDeduplication(logMsg);

    return g_pfnOriginalCreateFileW
        ? g_pfnOriginalCreateFileW(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile)
        : INVALID_HANDLE_VALUE;
}

static HANDLE WINAPI Hooked_CreateFileA(
    LPCSTR lpFileName,
    DWORD dwDesiredAccess,
    DWORD dwShareMode,
    LPSECURITY_ATTRIBUTES lpSecurityAttributes,
    DWORD dwCreationDisposition,
    DWORD dwFlagsAndAttributes,
    HANDLE hTemplateFile)
{
    wchar_t fileNameW[260] = {0};
    if (lpFileName)
    {
        MultiByteToWideChar(CP_ACP, 0, lpFileName, -1, fileNameW, 260);
    }

    wchar_t logMsg[512];
    if (lpFileName)
    {
        swprintf_s(logMsg, 512, L"[API] CreateFileA(fileName=\"%s\", access=0x%08X, creation=0x%08X)",
            fileNameW, dwDesiredAccess, dwCreationDisposition);
    }
    else
    {
        swprintf_s(logMsg, 512, L"[API] CreateFileA(fileName=NULL, access=0x%08X, creation=0x%08X)",
            dwDesiredAccess, dwCreationDisposition);
    }
    WriteApiLogWithDeduplication(logMsg);

    return g_pfnOriginalCreateFileA
        ? g_pfnOriginalCreateFileA(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile)
        : INVALID_HANDLE_VALUE;
}

static BOOL WINAPI Hooked_ReadFile(HANDLE hFile, LPVOID lpBuffer, DWORD nNumberOfBytesToRead, LPDWORD lpNumberOfBytesRead, LPOVERLAPPED lpOverlapped)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    BOOL result = g_pfnOriginalReadFile ? g_pfnOriginalReadFile(hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, lpOverlapped) : FALSE;
    if (result)
    {
        wchar_t logMsg[1024];
        if (callerAddress && moduleName[0] != L'\0')
        {
            swprintf_s(logMsg, 1024, L"[API] ReadFile(hFile=0x%p, bytesToRead=%lu) result: read %lu bytes <- %s+0x%p",
                hFile, nNumberOfBytesToRead, (lpNumberOfBytesRead ? *lpNumberOfBytesRead : 0), moduleName, callerAddress);
        }
        else
        {
            swprintf_s(logMsg, 1024, L"[API] ReadFile(hFile=0x%p, bytesToRead=%lu) result: read %lu bytes",
                hFile, nNumberOfBytesToRead, (lpNumberOfBytesRead ? *lpNumberOfBytesRead : 0));
        }
        WriteApiLogWithDeduplication(logMsg);
    }
    return result;
}

static BOOL WINAPI Hooked_WriteFile(HANDLE hFile, LPCVOID lpBuffer, DWORD nNumberOfBytesToWrite, LPDWORD lpNumberOfBytesWritten, LPOVERLAPPED lpOverlapped)
{
    BOOL result = g_pfnOriginalWriteFile ? g_pfnOriginalWriteFile(hFile, lpBuffer, nNumberOfBytesToWrite, lpNumberOfBytesWritten, lpOverlapped) : FALSE;
    if (result)
    {
        wchar_t logMsg[512];
        swprintf_s(logMsg, 512, L"[API] WriteFile(hFile=0x%p, bytesToWrite=%lu) result: wrote %lu bytes",
            hFile, nNumberOfBytesToWrite, (lpNumberOfBytesWritten ? *lpNumberOfBytesWritten : 0));
        WriteApiLogWithDeduplication(logMsg);
    }
    return result;
}

static BOOL WINAPI Hooked_DeleteFileW(LPCWSTR lpFileName)
{
    BOOL result = g_pfnOriginalDeleteFileW ? g_pfnOriginalDeleteFileW(lpFileName) : FALSE;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] DeleteFileW(fileName=\"%s\") result: %s", lpFileName ? lpFileName : L"NULL", result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_DeleteFileA(LPCSTR lpFileName)
{
    BOOL result = g_pfnOriginalDeleteFileA ? g_pfnOriginalDeleteFileA(lpFileName) : FALSE;
    wchar_t fileNameW[260] = {0};
    if (lpFileName)
    {
        MultiByteToWideChar(CP_ACP, 0, lpFileName, -1, fileNameW, 260);
    }
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] DeleteFileA(fileName=\"%s\") result: %s", fileNameW, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_MoveFileW(LPCWSTR lpExistingFileName, LPCWSTR lpNewFileName)
{
    BOOL result = g_pfnOriginalMoveFileW ? g_pfnOriginalMoveFileW(lpExistingFileName, lpNewFileName) : FALSE;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] MoveFileW(from=\"%s\", to=\"%s\") result: %s",
        lpExistingFileName ? lpExistingFileName : L"NULL", lpNewFileName ? lpNewFileName : L"NULL", result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_MoveFileA(LPCSTR lpExistingFileName, LPCSTR lpNewFileName)
{
    BOOL result = g_pfnOriginalMoveFileA ? g_pfnOriginalMoveFileA(lpExistingFileName, lpNewFileName) : FALSE;
    wchar_t fromW[260] = {0}, toW[260] = {0};
    if (lpExistingFileName) MultiByteToWideChar(CP_ACP, 0, lpExistingFileName, -1, fromW, 260);
    if (lpNewFileName) MultiByteToWideChar(CP_ACP, 0, lpNewFileName, -1, toW, 260);
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] MoveFileA(from=\"%s\", to=\"%s\") result: %s", fromW, toW, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_CopyFileW(LPCWSTR lpExistingFileName, LPCWSTR lpNewFileName, BOOL bFailIfExists)
{
    BOOL result = g_pfnOriginalCopyFileW ? g_pfnOriginalCopyFileW(lpExistingFileName, lpNewFileName, bFailIfExists) : FALSE;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] CopyFileW(from=\"%s\", to=\"%s\", failIfExists=%d) result: %s",
        lpExistingFileName ? lpExistingFileName : L"NULL", lpNewFileName ? lpNewFileName : L"NULL", bFailIfExists, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_CopyFileA(LPCSTR lpExistingFileName, LPCSTR lpNewFileName, BOOL bFailIfExists)
{
    BOOL result = g_pfnOriginalCopyFileA ? g_pfnOriginalCopyFileA(lpExistingFileName, lpNewFileName, bFailIfExists) : FALSE;
    wchar_t fromW[260] = {0}, toW[260] = {0};
    if (lpExistingFileName) MultiByteToWideChar(CP_ACP, 0, lpExistingFileName, -1, fromW, 260);
    if (lpNewFileName) MultiByteToWideChar(CP_ACP, 0, lpNewFileName, -1, toW, 260);
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] CopyFileA(from=\"%s\", to=\"%s\", failIfExists=%d) result: %s", fromW, toW, bFailIfExists, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

// kernel32.dll Hook 函數
static HMODULE WINAPI Hooked_LoadLibraryW(LPCWSTR lpLibFileName)
{
    HMODULE result = g_pfnOriginalLoadLibraryW ? g_pfnOriginalLoadLibraryW(lpLibFileName) : NULL;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] LoadLibraryW(libName=\"%s\") result: 0x%p", lpLibFileName ? lpLibFileName : L"NULL", result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static HMODULE WINAPI Hooked_LoadLibraryA(LPCSTR lpLibFileName)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    HMODULE result = g_pfnOriginalLoadLibraryA ? g_pfnOriginalLoadLibraryA(lpLibFileName) : NULL;
    wchar_t libNameW[260] = {0};
    if (lpLibFileName) MultiByteToWideChar(CP_ACP, 0, lpLibFileName, -1, libNameW, 260);
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] LoadLibraryA(libName=\"%s\") result: 0x%p <- %s+0x%p",
            libNameW, result, moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] LoadLibraryA(libName=\"%s\") result: 0x%p",
            libNameW, result);
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static FARPROC WINAPI Hooked_GetProcAddress(HMODULE hModule, LPCSTR lpProcName)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    FARPROC result = g_pfnOriginalGetProcAddress ? g_pfnOriginalGetProcAddress(hModule, lpProcName) : NULL;
    wchar_t procNameW[260] = {0};
    if (lpProcName) MultiByteToWideChar(CP_ACP, 0, lpProcName, -1, procNameW, 260);
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] GetProcAddress(hModule=0x%p, procName=\"%s\") result: 0x%p <- %s+0x%p",
            hModule, procNameW, result, moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] GetProcAddress(hModule=0x%p, procName=\"%s\") result: 0x%p",
            hModule, procNameW, result);
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static LPVOID WINAPI Hooked_VirtualAlloc(LPVOID lpAddress, SIZE_T dwSize, DWORD flAllocationType, DWORD flProtect)
{
    LPVOID result = g_pfnOriginalVirtualAlloc ? g_pfnOriginalVirtualAlloc(lpAddress, dwSize, flAllocationType, flProtect) : NULL;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] VirtualAlloc(address=0x%p, size=%llu, type=0x%08X, protect=0x%08X) result: 0x%p",
        lpAddress, (unsigned long long)dwSize, flAllocationType, flProtect, result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_VirtualFree(LPVOID lpAddress, SIZE_T dwSize, DWORD dwFreeType)
{
    BOOL result = g_pfnOriginalVirtualFree ? g_pfnOriginalVirtualFree(lpAddress, dwSize, dwFreeType) : FALSE;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] VirtualFree(address=0x%p, size=%llu, type=0x%08X) result: %s",
        lpAddress, (unsigned long long)dwSize, dwFreeType, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

// user32.dll Hook 函數
static LRESULT WINAPI Hooked_SendMessageW(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    LRESULT result = g_pfnOriginalSendMessageW ? g_pfnOriginalSendMessageW(hWnd, Msg, wParam, lParam) : 0;
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] SendMessageW(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: 0x%p <- %s+0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, (void*)result, moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] SendMessageW(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: 0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, (void*)result);
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static LRESULT WINAPI Hooked_SendMessageA(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    LRESULT result = g_pfnOriginalSendMessageA ? g_pfnOriginalSendMessageA(hWnd, Msg, wParam, lParam) : 0;
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] SendMessageA(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: 0x%p <- %s+0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, (void*)result, moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] SendMessageA(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: 0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, (void*)result);
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_PostMessageW(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    BOOL result = g_pfnOriginalPostMessageW ? g_pfnOriginalPostMessageW(hWnd, Msg, wParam, lParam) : FALSE;
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] PostMessageW(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: %s <- %s+0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, result ? L"成功" : L"失敗", moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] PostMessageW(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: %s",
            hWnd, Msg, (void*)wParam, (void*)lParam, result ? L"成功" : L"失敗");
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_PostMessageA(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    BOOL result = g_pfnOriginalPostMessageA ? g_pfnOriginalPostMessageA(hWnd, Msg, wParam, lParam) : FALSE;
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] PostMessageA(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: %s <- %s+0x%p",
            hWnd, Msg, (void*)wParam, (void*)lParam, result ? L"成功" : L"失敗", moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] PostMessageA(hWnd=0x%p, msg=0x%04X, wParam=0x%p, lParam=0x%p) result: %s",
            hWnd, Msg, (void*)wParam, (void*)lParam, result ? L"成功" : L"失敗");
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static HWND WINAPI Hooked_FindWindowW(LPCWSTR lpClassName, LPCWSTR lpWindowName)
{
    HWND result = g_pfnOriginalFindWindowW ? g_pfnOriginalFindWindowW(lpClassName, lpWindowName) : NULL;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] FindWindowW(className=\"%s\", windowName=\"%s\") result: 0x%p",
        lpClassName ? lpClassName : L"NULL", lpWindowName ? lpWindowName : L"NULL", result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static HWND WINAPI Hooked_FindWindowA(LPCSTR lpClassName, LPCSTR lpWindowName)
{
    HWND result = g_pfnOriginalFindWindowA ? g_pfnOriginalFindWindowA(lpClassName, lpWindowName) : NULL;
    wchar_t classNameW[260] = {0}, windowNameW[260] = {0};
    if (lpClassName) MultiByteToWideChar(CP_ACP, 0, lpClassName, -1, classNameW, 260);
    if (lpWindowName) MultiByteToWideChar(CP_ACP, 0, lpWindowName, -1, windowNameW, 260);
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] FindWindowA(className=\"%s\", windowName=\"%s\") result: 0x%p",
        classNameW, windowNameW, result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static int WINAPI Hooked_GetWindowTextW(HWND hWnd, LPWSTR lpString, int nMaxCount)
{
    int result = g_pfnOriginalGetWindowTextW ? g_pfnOriginalGetWindowTextW(hWnd, lpString, nMaxCount) : 0;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] GetWindowTextW(hWnd=0x%p, maxCount=%d) result: %d", hWnd, nMaxCount, result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static int WINAPI Hooked_GetWindowTextA(HWND hWnd, LPSTR lpString, int nMaxCount)
{
    int result = g_pfnOriginalGetWindowTextA ? g_pfnOriginalGetWindowTextA(hWnd, lpString, nMaxCount) : 0;
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] GetWindowTextA(hWnd=0x%p, maxCount=%d) result: %d", hWnd, nMaxCount, result);
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static BOOL WINAPI Hooked_SetWindowTextW(HWND hWnd, LPCWSTR lpString)
{
    wchar_t moduleName[260] = {0};
    void* callerAddress = NULL;
    GetCallerInfo(moduleName, 260, &callerAddress);

    BOOL result = g_pfnOriginalSetWindowTextW ? g_pfnOriginalSetWindowTextW(hWnd, lpString) : FALSE;
    wchar_t logMsg[1024];
    if (callerAddress && moduleName[0] != L'\0')
    {
        swprintf_s(logMsg, 1024, L"[API] SetWindowTextW(hWnd=0x%p, text=\"%s\") result: %s <- %s+0x%p",
            hWnd, lpString ? lpString : L"NULL", result ? L"成功" : L"失敗", moduleName, callerAddress);
    }
    else
    {
        swprintf_s(logMsg, 1024, L"[API] SetWindowTextW(hWnd=0x%p, text=\"%s\") result: %s",
            hWnd, lpString ? lpString : L"NULL", result ? L"成功" : L"失敗");
    }
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

// ============================================================
// 診斷用（暫時）：攔截自繪控制項（GXWND）WM_PAINT 期間呼叫的
// GDI 文字繪製函式，找出摘要真實文字（例：Machine / 使用者輸入值）
// 是用哪個函式、什麼字串畫出來的。找到答案後應移除，非正式功能。
// ============================================================
typedef BOOL (WINAPI *PFN_TextOutW)(HDC, int, int, LPCWSTR, int);
typedef BOOL (WINAPI *PFN_ExtTextOutW)(HDC, int, int, UINT, const RECT*, LPCWSTR, UINT, const INT*);
typedef int  (WINAPI *PFN_DrawTextW)(HDC, LPCWSTR, int, LPRECT, UINT);
typedef HDC  (WINAPI *PFN_BeginPaint)(HWND, LPPAINTSTRUCT);
typedef BOOL (WINAPI *PFN_EndPaint)(HWND, const PAINTSTRUCT*);

typedef BOOL (WINAPI *PFN_TextOutA)(HDC, int, int, LPCSTR, int);
typedef BOOL (WINAPI *PFN_ExtTextOutA)(HDC, int, int, UINT, const RECT*, LPCSTR, UINT, const INT*);
typedef int  (WINAPI *PFN_DrawTextA)(HDC, LPCSTR, int, LPRECT, UINT);
typedef HDC  (WINAPI *PFN_GetDC)(HWND);
typedef int  (WINAPI *PFN_ReleaseDC)(HWND, HDC);

static PFN_TextOutW g_pfnOriginalTextOutW = NULL;
static PFN_ExtTextOutW g_pfnOriginalExtTextOutW = NULL;
static PFN_DrawTextW g_pfnOriginalDrawTextW = NULL;
static PFN_TextOutA g_pfnOriginalTextOutA = NULL;
static PFN_ExtTextOutA g_pfnOriginalExtTextOutA = NULL;
static PFN_DrawTextA g_pfnOriginalDrawTextA = NULL;
static PFN_BeginPaint g_pfnOriginalBeginPaint = NULL;
static PFN_EndPaint g_pfnOriginalEndPaint = NULL;
static PFN_GetDC g_pfnOriginalGetDC = NULL;
static PFN_ReleaseDC g_pfnOriginalReleaseDC = NULL;

// 不再靠 BeginPaint/EndPaint 配對特定 hwnd/hdc（GXWND 可能不走這條路），
// 改用時間窗全域旗標：CaptureGxwndPaint() 強制重繪前打開、之後關閉，
// 這段時間窗內任何文字繪製呼叫全部記錄，事後人工比對哪筆是目標文字。
//
// 教訓（2026-07-29 InSpec 實測崩潰兩次後查出）：hook 函式可能被多個執行緒
// 併發呼叫（InSpec 有獨立的「攝影機/探針」子視窗，很可能自己有訊息迴圈），
// 若多執行緒同時對同一個 FILE* 呼叫 fwprintf/fflush 沒有加鎖，會腐蝕 CRT
// 內部緩衝區狀態、造成存取違規——這正是既有 EnableApiHook 那套 log 機制
// 要用 CRITICAL_SECTION + 佇列 + 專屬寫入執行緒的理由，這裡的新 hook 一開始
// 沒跟進同一做法，是實作疏漏，不是這個技術路線本身不安全，現在補上鎖。
static CRITICAL_SECTION g_csCapture;
static bool g_csCaptureInited = false;
static volatile HWND g_captureTargetWnd = NULL;
static volatile bool g_capturing = false;
static FILE* g_captureFile = NULL;

static void CaptureLog(const wchar_t* fmt, ...)
{
    if (!g_capturing || !g_captureFile || !g_csCaptureInited) return;
    EnterCriticalSection(&g_csCapture);
    va_list args;
    va_start(args, fmt);
    vfwprintf(g_captureFile, fmt, args);
    va_end(args);
    fflush(g_captureFile);
    LeaveCriticalSection(&g_csCapture);
}

// 摘要面板「輸入值」文字即時快取——InSpec 自己重繪摘要面板時順手記下最新值，
// 供就地編輯欄位預填目前值。x 座標容忍帶（原本寫死 50，實機查出這個 DPI/字型情境下
// 實際是 45，這次改成範圍比對，不再假設單一固定值）。
static CRITICAL_SECTION g_csValueText;
static bool g_csValueTextInited = false;
static wchar_t g_lastValueText[128] = L"";
static volatile HWND g_summaryGridCached = NULL;
static HWND g_valueEdit = NULL;

static void RememberValueTextIfMatch(HDC hdc, int x, int y, LPCWSTR text, int len)
{
    if (!g_summaryGridCached || x < 40 || x > 55 || y < 50 || y > 60) return;
    if (WindowFromDC(hdc) != g_summaryGridCached) return;
    if (!g_csValueTextInited) { InitializeCriticalSection(&g_csValueText); g_csValueTextInited = true; }
    EnterCriticalSection(&g_csValueText);
    int n = len < 127 ? len : 127;
    if (n < 0) n = 0;
    wcsncpy_s(g_lastValueText, 128, text, n);
    LeaveCriticalSection(&g_csValueText);

    if (g_valueEdit && IsWindow(g_valueEdit) && GetFocus() != g_valueEdit)
        InvalidateRect(g_valueEdit, NULL, TRUE);
}

static HDC WINAPI Hooked_BeginPaint(HWND hWnd, LPPAINTSTRUCT lpPaint)
{
    HDC hdc = g_pfnOriginalBeginPaint ? g_pfnOriginalBeginPaint(hWnd, lpPaint) : NULL;
    CaptureLog(L"BeginPaint hwnd=0x%p hdc=0x%p match=%d\n", hWnd, hdc, hWnd == g_captureTargetWnd);
    return hdc;
}

static BOOL WINAPI Hooked_EndPaint(HWND hWnd, const PAINTSTRUCT* lpPaint)
{
    return g_pfnOriginalEndPaint ? g_pfnOriginalEndPaint(hWnd, lpPaint) : FALSE;
}

static HDC WINAPI Hooked_GetDC(HWND hWnd)
{
    HDC hdc = g_pfnOriginalGetDC ? g_pfnOriginalGetDC(hWnd) : NULL;
    CaptureLog(L"GetDC hwnd=0x%p hdc=0x%p match=%d\n", hWnd, hdc, hWnd == g_captureTargetWnd);
    return hdc;
}

static int WINAPI Hooked_ReleaseDC(HWND hWnd, HDC hdc)
{
    return g_pfnOriginalReleaseDC ? g_pfnOriginalReleaseDC(hWnd, hdc) : 0;
}

static BOOL WINAPI Hooked_TextOutW(HDC hdc, int x, int y, LPCWSTR text, int c)
{
    if (text && c > 0)
    {
        CaptureLog(L"TextOutW hdc=0x%p (%d,%d) len=%d \"%.*s\"\n", hdc, x, y, c, c, text);
        RememberValueTextIfMatch(hdc, x, y, text, c);
    }
    return g_pfnOriginalTextOutW ? g_pfnOriginalTextOutW(hdc, x, y, text, c) : FALSE;
}

static BOOL WINAPI Hooked_ExtTextOutW(HDC hdc, int x, int y, UINT opt, const RECT* rc, LPCWSTR text, UINT c, const INT* dx)
{
    if (text && c > 0)
    {
        CaptureLog(L"ExtTextOutW hdc=0x%p (%d,%d) len=%u \"%.*s\"\n", hdc, x, y, c, (int)c, text);
        RememberValueTextIfMatch(hdc, x, y, text, (int)c);
    }
    return g_pfnOriginalExtTextOutW ? g_pfnOriginalExtTextOutW(hdc, x, y, opt, rc, text, c, dx) : FALSE;
}

static int WINAPI Hooked_DrawTextW(HDC hdc, LPCWSTR text, int c, LPRECT rc, UINT fmt)
{
    if (text)
    {
        int len = c < 0 ? (int)wcslen(text) : c;
        CaptureLog(L"DrawTextW hdc=0x%p len=%d \"%.*s\"\n", hdc, len, len, text);
    }
    return g_pfnOriginalDrawTextW ? g_pfnOriginalDrawTextW(hdc, text, c, rc, fmt) : 0;
}

static BOOL WINAPI Hooked_TextOutA(HDC hdc, int x, int y, LPCSTR text, int c)
{
    if (text && c > 0)
    {
        wchar_t buf[512] = {0};
        int n = MultiByteToWideChar(CP_ACP, 0, text, c, buf, 511);
        CaptureLog(L"TextOutA hdc=0x%p (%d,%d) len=%d \"%.*s\"\n", hdc, x, y, n, n, buf);
    }
    return g_pfnOriginalTextOutA ? g_pfnOriginalTextOutA(hdc, x, y, text, c) : FALSE;
}

static BOOL WINAPI Hooked_ExtTextOutA(HDC hdc, int x, int y, UINT opt, const RECT* rc, LPCSTR text, UINT c, const INT* dx)
{
    if (text && c > 0)
    {
        wchar_t buf[512] = {0};
        int n = MultiByteToWideChar(CP_ACP, 0, text, (int)c, buf, 511);
        CaptureLog(L"ExtTextOutA hdc=0x%p (%d,%d) len=%d \"%.*s\"\n", hdc, x, y, n, n, buf);
    }
    return g_pfnOriginalExtTextOutA ? g_pfnOriginalExtTextOutA(hdc, x, y, opt, rc, text, c, dx) : FALSE;
}

static int WINAPI Hooked_DrawTextA(HDC hdc, LPCSTR text, int c, LPRECT rc, UINT fmt)
{
    if (text)
    {
        int len = c < 0 ? (int)strlen(text) : c;
        wchar_t buf[512] = {0};
        int n = MultiByteToWideChar(CP_ACP, 0, text, len, buf, 511);
        CaptureLog(L"DrawTextA hdc=0x%p len=%d \"%.*s\"\n", hdc, n, n, buf);
    }
    return g_pfnOriginalDrawTextA ? g_pfnOriginalDrawTextA(hdc, text, c, rc, fmt) : 0;
}

static BOOL WINAPI Hooked_SetWindowTextA(HWND hWnd, LPCSTR lpString)
{
    BOOL result = g_pfnOriginalSetWindowTextA ? g_pfnOriginalSetWindowTextA(hWnd, lpString) : FALSE;
    wchar_t textW[260] = {0};
    if (lpString) MultiByteToWideChar(CP_ACP, 0, lpString, -1, textW, 260);
    wchar_t logMsg[512];
    swprintf_s(logMsg, 512, L"[API] SetWindowTextA(hWnd=0x%p, text=\"%s\") result: %s",
        hWnd, textW, result ? L"成功" : L"失敗");
    WriteApiLogWithDeduplication(logMsg);
    return result;
}

static void InstallAllHooks()
{
    HMODULE modules[1024] = {0};
    DWORD needed = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules, sizeof(modules), &needed))
    {
        return;
    }

    DWORD moduleCount = needed / sizeof(HMODULE);
    struct HookTarget
    {
        const char* importedDllLower;
        const char* funcName;
        void* hookFunc;
        void** originalOut;
    };

    HookTarget targets[] =
    {
        // kernel32.dll - 文件操作
        { "kernel32.dll", "CreateFileW", (void*)Hooked_CreateFileW, (void**)&g_pfnOriginalCreateFileW },
        { "kernel32.dll", "CreateFileA", (void*)Hooked_CreateFileA, (void**)&g_pfnOriginalCreateFileA },
        { "kernel32.dll", "ReadFile", (void*)Hooked_ReadFile, (void**)&g_pfnOriginalReadFile },
        { "kernel32.dll", "WriteFile", (void*)Hooked_WriteFile, (void**)&g_pfnOriginalWriteFile },
        { "kernel32.dll", "DeleteFileW", (void*)Hooked_DeleteFileW, (void**)&g_pfnOriginalDeleteFileW },
        { "kernel32.dll", "DeleteFileA", (void*)Hooked_DeleteFileA, (void**)&g_pfnOriginalDeleteFileA },
        { "kernel32.dll", "MoveFileW", (void*)Hooked_MoveFileW, (void**)&g_pfnOriginalMoveFileW },
        { "kernel32.dll", "MoveFileA", (void*)Hooked_MoveFileA, (void**)&g_pfnOriginalMoveFileA },
        { "kernel32.dll", "CopyFileW", (void*)Hooked_CopyFileW, (void**)&g_pfnOriginalCopyFileW },
        { "kernel32.dll", "CopyFileA", (void*)Hooked_CopyFileA, (void**)&g_pfnOriginalCopyFileA },
        // kernel32.dll - 模組和記憶體操作
        { "kernel32.dll", "LoadLibraryW", (void*)Hooked_LoadLibraryW, (void**)&g_pfnOriginalLoadLibraryW },
        { "kernel32.dll", "LoadLibraryA", (void*)Hooked_LoadLibraryA, (void**)&g_pfnOriginalLoadLibraryA },
        { "kernel32.dll", "GetProcAddress", (void*)Hooked_GetProcAddress, (void**)&g_pfnOriginalGetProcAddress },
        { "kernel32.dll", "VirtualAlloc", (void*)Hooked_VirtualAlloc, (void**)&g_pfnOriginalVirtualAlloc },
        { "kernel32.dll", "VirtualFree", (void*)Hooked_VirtualFree, (void**)&g_pfnOriginalVirtualFree },
        // user32.dll - 視窗訊息（記錄觸發訊息）
        { "user32.dll", "SendMessageW", (void*)Hooked_SendMessageW, (void**)&g_pfnOriginalSendMessageW },
        { "user32.dll", "SendMessageA", (void*)Hooked_SendMessageA, (void**)&g_pfnOriginalSendMessageA },
        { "user32.dll", "PostMessageW", (void*)Hooked_PostMessageW, (void**)&g_pfnOriginalPostMessageW },
        { "user32.dll", "PostMessageA", (void*)Hooked_PostMessageA, (void**)&g_pfnOriginalPostMessageA },
        // user32.dll - 視窗操作
        { "user32.dll", "FindWindowW", (void*)Hooked_FindWindowW, (void**)&g_pfnOriginalFindWindowW },
        { "user32.dll", "FindWindowA", (void*)Hooked_FindWindowA, (void**)&g_pfnOriginalFindWindowA },
        { "user32.dll", "GetWindowTextW", (void*)Hooked_GetWindowTextW, (void**)&g_pfnOriginalGetWindowTextW },
        { "user32.dll", "GetWindowTextA", (void*)Hooked_GetWindowTextA, (void**)&g_pfnOriginalGetWindowTextA },
        { "user32.dll", "SetWindowTextW", (void*)Hooked_SetWindowTextW, (void**)&g_pfnOriginalSetWindowTextW },
        { "user32.dll", "SetWindowTextA", (void*)Hooked_SetWindowTextA, (void**)&g_pfnOriginalSetWindowTextA },
    };

    for (DWORD i = 0; i < moduleCount; i++)
    {
        HMODULE mod = modules[i];
        if (!mod) continue;
        if (g_hSelfModule && mod == g_hSelfModule) continue;

        for (int j = 0; j < sizeof(targets) / sizeof(targets[0]); j++)
        {
            PatchIatForOneFunction(mod, targets[j].importedDllLower, targets[j].funcName, targets[j].hookFunc, targets[j].originalOut);
        }
    }
}

// 教訓（2026-07-29 二度崩潰後查出）：GXWND 文字擷取只需要這 10 個 GDI/繪圖相關
// hook，之前 CaptureGxwndPaint 借用 EnableApiHook 的完整 hook 集合（含
// SendMessageW/PostMessageW/GetWindowTextW 等——每次呼叫都做 GetCallerInfo()
// 堆疊查詢，開銷大、範圍廣），裝上後從未卸載，疊加後續正常操作（例如再次
// 用 SendMessageW 觸發工具列按鈕）時導致 InSpec 崩潰。改成獨立、範圍最小的
// hook 集合，用獨立旗標（g_gdiHooksInstalled）追蹤，不跟 EnableApiHook 共用。
static bool g_gdiHooksInstalled = false;

static void InstallGdiCaptureHooks()
{
    HMODULE modules[1024] = {0};
    DWORD needed = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules, sizeof(modules), &needed))
    {
        return;
    }
    DWORD moduleCount = needed / sizeof(HMODULE);

    struct HookTarget
    {
        const char* importedDllLower;
        const char* funcName;
        void* hookFunc;
        void** originalOut;
    };

    HookTarget targets[] =
    {
        { "user32.dll", "BeginPaint", (void*)Hooked_BeginPaint, (void**)&g_pfnOriginalBeginPaint },
        { "user32.dll", "EndPaint", (void*)Hooked_EndPaint, (void**)&g_pfnOriginalEndPaint },
        { "user32.dll", "GetDC", (void*)Hooked_GetDC, (void**)&g_pfnOriginalGetDC },
        { "user32.dll", "ReleaseDC", (void*)Hooked_ReleaseDC, (void**)&g_pfnOriginalReleaseDC },
        { "gdi32.dll", "TextOutW", (void*)Hooked_TextOutW, (void**)&g_pfnOriginalTextOutW },
        { "gdi32.dll", "ExtTextOutW", (void*)Hooked_ExtTextOutW, (void**)&g_pfnOriginalExtTextOutW },
        { "user32.dll", "DrawTextW", (void*)Hooked_DrawTextW, (void**)&g_pfnOriginalDrawTextW },
        { "gdi32.dll", "TextOutA", (void*)Hooked_TextOutA, (void**)&g_pfnOriginalTextOutA },
        { "gdi32.dll", "ExtTextOutA", (void*)Hooked_ExtTextOutA, (void**)&g_pfnOriginalExtTextOutA },
        { "user32.dll", "DrawTextA", (void*)Hooked_DrawTextA, (void**)&g_pfnOriginalDrawTextA },
    };

    for (DWORD i = 0; i < moduleCount; i++)
    {
        HMODULE mod = modules[i];
        if (!mod) continue;
        if (g_hSelfModule && mod == g_hSelfModule) continue;

        for (int j = 0; j < sizeof(targets) / sizeof(targets[0]); j++)
        {
            PatchIatForOneFunction(mod, targets[j].importedDllLower, targets[j].funcName, targets[j].hookFunc, targets[j].originalOut);
        }
    }
    g_gdiHooksInstalled = true;
}

// ============================================================
// 摘要改造模組 (Summary Patch)
// 量測完成時，在摘要面板上覆蓋一個 GDI 自繪的大打勾 (Lucide circle-check-big)
// 依據實測：摘要對話框 = SysTabControl32(1251) 下的 #32770，內含 GXWND(id 1268)；
//          control 1264 的可見性 = InSpec 自身的「量測完成」訊號。
// 策略：建立一個覆蓋層子視窗蓋住摘要面板，完成時顯示、未完成時隱藏，
//       完全不改動 InSpec 既有控制項（最不侵入、可隨時關閉還原）。
// ============================================================

static const wchar_t* SUMMARY_OVERLAY_CLASS = L"InSpecSummaryCheckOverlay";
static const wchar_t* DEMO_BTN_CLASS = L"InSpecDemoButton";
static const wchar_t* RELOAD_MASK_CLASS = L"InSpecReloadMask";
static const wchar_t* RELOAD_BTN_CLASS = L"InSpecReloadButton";
static const int SUMMARY_GRID_ID = 1268;   // GXWND，用來錨定摘要對話框
static const int SUMMARY_DONE_ID = 1264;   // 綠勾指示器，可見 = 完成
static const int TOOL_ANCHOR_ID  = 1166;   // 「元素」右側量測工具列(錨點，用於放置 demo 按鈕)
static const UINT SUMMARY_POLL_MS = 300;

// 監看執行緒（打勾覆蓋層 + demo 按鈕 + XYZ 就地編輯 + 重新載入按鈕/蒙版共用一條）
static volatile bool g_monitorRunning = false;   // 執行緒存活旗標
static volatile bool g_overlayEnabled = false;   // 摘要打勾功能開關
static volatile bool g_demoEnabled = false;      // demo 按鈕功能開關
static volatile bool g_liveEditEnabled = false;  // 即時讀寫注入按鈕功能開關
static volatile bool g_xyzEditEnabled = false;   // X/Y/Z 座標就地編輯功能開關
static volatile bool g_reloadBtnEnabled = false; // 重新載入按鈕功能開關
static HANDLE g_hMonitorThread = NULL;
static DWORD  g_monitorThreadId = 0;
static HWND g_summaryDlg = NULL;
static HWND g_summaryOverlay = NULL;
static HWND g_demoBtn = NULL;
static HWND g_liveEditBtn = NULL;
static HWND g_xyzEdit[3] = { NULL, NULL, NULL };       // X/Y/Z 各自的就地編輯 Edit 控制項
static HWND g_reloadBtn = NULL;
static HWND g_reloadMaskWnd = NULL;
static volatile bool g_reloadMaskActive = false; // 蒙版動畫播放中——StopMonitorIfIdle 要看這個旗標，不能在播放中被關掉執行緒
static bool g_classesRegistered = false;

// 覆蓋層樣式：展示「可換掉 InSpec 前端顯示」的能力，樣式由 host 端呼叫 Set* 匯出決定，
// 不是寫死只有打勾一種。0=打勾 1=叉叉 2=自訂文字，顏色/文字各自可覆寫。
enum OverlaySymbolMode { OverlaySymbol_Check = 0, OverlaySymbol_Cross = 1, OverlaySymbol_CustomText = 2 };
static int g_overlaySymbolMode = OverlaySymbol_Check;
static COLORREF g_overlaySymbolColor = RGB(22, 163, 74); // 預設綠（原打勾色）
static wchar_t g_overlayCustomText[64] = L"";

// 依 Lucide circle-check-big／circle-x 的路徑，在 rc 內置中畫出打勾／叉叉／自訂文字
static void DrawOverlaySymbol(HDC hdc, const RECT& rc)
{
    int w = rc.right - rc.left;
    int h = rc.bottom - rc.top;
    if (w <= 4 || h <= 4) return;

    // 背景填白（摘要面板底色）
    HBRUSH bg = CreateSolidBrush(RGB(255, 255, 255));
    FillRect(hdc, &rc, bg);
    DeleteObject(bg);

    if (g_overlaySymbolMode == OverlaySymbol_CustomText)
    {
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, g_overlaySymbolColor);
        HFONT font = CreateFontW(-(int)(h * 0.35), 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
            DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
        HGDIOBJ oldFont = SelectObject(hdc, font);
        RECT textRc = rc;
        DrawTextW(hdc, g_overlayCustomText, -1, &textRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        SelectObject(hdc, oldFont);
        DeleteObject(font);
        return;
    }

    // 24x24 viewBox 置中，取 0.82 內縮（打勾／叉叉共用）
    double side = (w < h ? w : h) * 0.82;
    double scale = side / 24.0;
    double ox = rc.left + (w - side) / 2.0;
    double oy = rc.top + (h - side) / 2.0;
    #define MAPX(px) ((int)(ox + (px) * scale + 0.5))
    #define MAPY(py) ((int)(oy + (py) * scale + 0.5))

    int penW = (int)(2.2 * scale + 0.5);
    if (penW < 2) penW = 2;

    LOGBRUSH lb; lb.lbStyle = BS_SOLID; lb.lbColor = g_overlaySymbolColor; lb.lbHatch = 0;
    HPEN pen = ExtCreatePen(PS_GEOMETRIC | PS_SOLID | PS_ENDCAP_ROUND | PS_JOIN_ROUND,
                            penW, &lb, 0, NULL);
    HGDIOBJ oldPen = SelectObject(hdc, pen);
    HGDIOBJ oldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));
    int oldBk = SetBkMode(hdc, TRANSPARENT);

    // 圓 (center 12,12 r10)
    Ellipse(hdc, MAPX(2), MAPY(2), MAPX(22), MAPY(22));

    if (g_overlaySymbolMode == OverlaySymbol_Cross)
    {
        // 叉叉：兩條對角線
        MoveToEx(hdc, MAPX(7), MAPY(7), NULL);
        LineTo(hdc, MAPX(17), MAPY(17));
        MoveToEx(hdc, MAPX(17), MAPY(7), NULL);
        LineTo(hdc, MAPX(7), MAPY(17));
    }
    else
    {
        // 打勾 path "m9 11 3 3L22 4"  => (9,11)->(12,14)->(22,4)
        POINT pts[3] = { { MAPX(9), MAPY(11) }, { MAPX(12), MAPY(14) }, { MAPX(22), MAPY(4) } };
        Polyline(hdc, pts, 3);
    }

    SetBkMode(hdc, oldBk);
    SelectObject(hdc, oldBrush);
    SelectObject(hdc, oldPen);
    DeleteObject(pen);
    #undef MAPX
    #undef MAPY
}

static LRESULT CALLBACK SummaryOverlayProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_ERASEBKGND:
        return 1; // 交給 WM_PAINT 一次畫完，避免閃爍
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc; GetClientRect(hWnd, &rc);
        DrawOverlaySymbol(hdc, rc);
        EndPaint(hWnd, &ps);
        return 0;
    }
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

// ---- demo 按鈕：按下彈出訊息視窗 ----
static LRESULT CALLBACK DemoButtonProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc; GetClientRect(hWnd, &rc);
        FillRect(hdc, &rc, GetSysColorBrush(COLOR_BTNFACE));
        DrawEdge(hdc, &rc, EDGE_RAISED, BF_RECT);
        SetBkMode(hdc, TRANSPARENT);
        DrawTextW(hdc, L"Demo", -1, &rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        EndPaint(hWnd, &ps);
        return 0;
    }
    case WM_LBUTTONUP:
        // 內容 "Demo功能請輸入Run" — 以 \u 轉義避免 cp950 誤讀原始碼 CJK
        MessageBoxW(hWnd, L"Demo功能請輸入Run", L"Demo",
                    MB_OK | MB_ICONINFORMATION);
        return 0;
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

// 找 demo 按鈕的錨點工具列（class=ToolbarWindow32 且 id=1166）
static HWND g_foundTool = NULL;
static BOOL CALLBACK FindToolProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"ToolbarWindow32") == 0 && GetDlgCtrlID(hWnd) == TOOL_ANCHOR_ID)
    {
        g_foundTool = hWnd;
        return FALSE;
    }
    return TRUE;
}
static BOOL CALLBACK FindMainForToolProc(HWND hWnd, LPARAM lParam)
{
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"InSpecAppFrame") == 0)
    {
        g_foundTool = NULL;
        EnumChildWindows(hWnd, FindToolProc, 0);
        if (g_foundTool)
        {
            *((HWND*)lParam) = g_foundTool;
            return FALSE;
        }
    }
    return TRUE;
}
static HWND FindToolAnchor()
{
    HWND t = NULL;
    EnumWindows(FindMainForToolProc, (LPARAM)&t);
    return t;
}

// 找出摘要對話框：主視窗底下 class=GXWND 且 id=1268 的父視窗
static HWND g_foundGrid = NULL;
static BOOL CALLBACK FindGridProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"GXWND") == 0 && GetDlgCtrlID(hWnd) == SUMMARY_GRID_ID)
    {
        g_foundGrid = hWnd;
        return FALSE;
    }
    return TRUE;
}

static BOOL CALLBACK FindMainProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"InSpecAppFrame") == 0)
    {
        g_foundGrid = NULL;
        EnumChildWindows(hWnd, FindGridProc, 0);
        if (g_foundGrid)
        {
            *((HWND*)lParam) = g_foundGrid;
            return FALSE;
        }
    }
    return TRUE;
}

static HWND FindSummaryDialog()
{
    HWND grid = NULL;
    EnumWindows(FindMainProc, (LPARAM)&grid);
    if (!grid) return NULL;
    return GetParent(grid); // #32770 摘要對話框
}

// 找出摘要對話框內的 GXWND 本身（不像 FindSummaryDialog 再往上取 GetParent）
static HWND FindSummaryGrid()
{
    HWND grid = NULL;
    EnumWindows(FindMainProc, (LPARAM)&grid);
    return grid;
}

__declspec(dllexport) bool CaptureGxwndPaint()
{
    HWND grid = FindSummaryGrid();
    if (!grid) return false;

    if (!g_csCaptureInited)
    {
        InitializeCriticalSection(&g_csCapture);
        g_csCaptureInited = true;
    }

    _wfopen_s(&g_captureFile, L"C:\\InSpecHookDLL_paint.txt", L"w, ccs=UTF-8");
    if (!g_captureFile) return false;

    if (!g_gdiHooksInstalled)
    {
        InstallGdiCaptureHooks();
    }

    g_captureTargetWnd = grid;
    g_capturing = true;
    // 不再用 RDW_ALLCHILDREN 強制整棵子視窗樹重繪——2026-07-29 InSpec 實測崩潰後
    // 確認根因是 hook 內未加鎖的併發檔案寫入（已修正，見 CaptureLog），但仍收斂
    // 觸發範圍：只重繪目標本身，不刻意波及其他控制項，降低不必要的攻擊面。
    InvalidateRect(grid, NULL, TRUE);
    UpdateWindow(grid);
    Sleep(300);
    g_capturing = false;
    g_captureTargetWnd = NULL;

    EnterCriticalSection(&g_csCapture);
    fclose(g_captureFile);
    g_captureFile = NULL;
    LeaveCriticalSection(&g_csCapture);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_CaptureGxwndPaint(LPVOID lpParam)
{
    (void)lpParam;
    return CaptureGxwndPaint() ? 1 : 0;
}

// ============================================================
// 找到 InSpec 的「輸入提示」對話框，把呼叫方傳入的值填進
// 多行 Edit 控制項、對「確定」鈕送 BM_CLICK——全程訊息注入，
// 不依賴合成滑鼠座標點擊（已驗證對此類對話框無效）。
// ============================================================
static HWND g_foundEdit = NULL;
static HWND g_foundOkBtn = NULL;

static BOOL CALLBACK FindPromptChildrenProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    wchar_t txt[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    GetWindowTextW(hWnd, txt, 63);
    if (wcscmp(cls, L"Edit") == 0 && !g_foundEdit)
    {
        g_foundEdit = hWnd;
    }
    else if (wcscmp(cls, L"Button") == 0 && !g_foundOkBtn)
    {
        g_foundOkBtn = hWnd; // 這個對話框只有一顆按鈕（確定），第一個 Button 即是
    }
    return TRUE;
}

__declspec(dllexport) bool AutoFillAndConfirmPrompt(const wchar_t* value)
{
    HWND dlg = FindWindowW(NULL, L"輸入提示");
    if (!dlg) return false;

    g_foundEdit = NULL;
    g_foundOkBtn = NULL;
    EnumChildWindows(dlg, FindPromptChildrenProc, 0);

    if (g_foundEdit && value)
    {
        SendMessageW(g_foundEdit, WM_SETTEXT, 0, (LPARAM)value);
    }
    if (g_foundOkBtn)
    {
        SendMessageW(g_foundOkBtn, BM_CLICK, 0, 0);
        return true;
    }
    return false;
}

// lpParam：呼叫方（注入器）需先在目標行程配置記憶體、寫入 UTF-16 字串，
// 再把該記憶體位址當作 CreateRemoteThread 的 lpParameter 傳入——標準跨行程傳字串手法。
__declspec(dllexport) DWORD WINAPI ThreadWrapper_AutoFillAndConfirmPrompt(LPVOID lpParam)
{
    return AutoFillAndConfirmPrompt((const wchar_t*)lpParam) ? 1 : 0;
}

// ============================================================
// 診斷用（暫時）：列舉「元素」右側量測工具列（ToolbarWindow32/1166）
// 全部按鈕的 index/command-id/rect/state，寫檔供人工比對哪個是「執行」，
// 並提供 ClickToolButtonByIndex 用 WM_LBUTTONDOWN/UP 相對座標訊息注入
// 點擊——不依賴合成滑鼠座標點擊（已驗證對部分控制項不可靠）。
// ============================================================
static BOOL CALLBACK DumpAllToolbarsProc(HWND hWnd, LPARAM lParam)
{
    FILE* f = (FILE*)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"ToolbarWindow32") == 0)
    {
        int id = GetDlgCtrlID(hWnd);
        int count = (int)SendMessageW(hWnd, TB_BUTTONCOUNT, 0, 0);
        RECT wrc = {0};
        GetWindowRect(hWnd, &wrc);
        fwprintf(f, L"ToolbarWindow32 hwnd=0x%p id=%d buttonCount=%d screenRect=(%ld,%ld,%ld,%ld)\n",
            hWnd, id, count, wrc.left, wrc.top, wrc.right, wrc.bottom);
    }
    EnumChildWindows(hWnd, DumpAllToolbarsProc, lParam);
    return TRUE;
}

__declspec(dllexport) bool DumpAllToolbars()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;

    FILE* f = NULL;
    _wfopen_s(&f, L"C:\\InSpecHookDLL_alltoolbars.txt", L"w, ccs=UTF-8");
    if (!f) return false;

    EnumChildWindows(main, DumpAllToolbarsProc, (LPARAM)f);

    fclose(f);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpAllToolbars(LPVOID lpParam)
{
    (void)lpParam;
    return DumpAllToolbars() ? 1 : 0;
}

// 找特定 id 的 ToolbarWindow32（不限定 InSpecAppFrame 底下第一層，遞迴找）
static HWND g_foundToolbarById = NULL;
static int g_wantToolbarId = 0;
static BOOL CALLBACK FindToolbarByIdProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"ToolbarWindow32") == 0 && GetDlgCtrlID(hWnd) == g_wantToolbarId)
    {
        g_foundToolbarById = hWnd;
    }
    EnumChildWindows(hWnd, FindToolbarByIdProc, 0);
    return TRUE;
}
static HWND FindToolbarById(int id)
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return NULL;
    g_foundToolbarById = NULL;
    g_wantToolbarId = id;
    EnumChildWindows(main, FindToolbarByIdProc, 0);
    return g_foundToolbarById;
}

__declspec(dllexport) bool DumpToolbar1165()
{
    HWND tb = FindToolbarById(1165);
    if (!tb) return false;

    FILE* f = NULL;
    _wfopen_s(&f, L"C:\\InSpecHookDLL_toolbar1165.txt", L"w, ccs=UTF-8");
    if (!f) return false;

    int count = (int)SendMessageW(tb, TB_BUTTONCOUNT, 0, 0);
    fwprintf(f, L"toolbar1165 hwnd=0x%p buttonCount=%d\n", tb, count);
    for (int i = 0; i < count; i++)
    {
        TBBUTTON btn = {0};
        SendMessageW(tb, TB_GETBUTTON, (WPARAM)i, (LPARAM)&btn);
        RECT rc = {0};
        SendMessageW(tb, TB_GETITEMRECT, (WPARAM)i, (LPARAM)&rc);
        fwprintf(f, L"[%d] idCommand=%d state=0x%02X style=0x%02X rect=(%ld,%ld,%ld,%ld)\n",
            i, btn.idCommand, btn.fsState, btn.fsStyle, rc.left, rc.top, rc.right, rc.bottom);
    }
    fclose(f);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpToolbar1165(LPVOID lpParam)
{
    (void)lpParam;
    return DumpToolbar1165() ? 1 : 0;
}

__declspec(dllexport) bool ClickToolbar1165ButtonByIndex(int index)
{
    HWND tb = FindToolbarById(1165);
    if (!tb) return false;
    RECT rc = {0};
    if (!SendMessageW(tb, TB_GETITEMRECT, (WPARAM)index, (LPARAM)&rc)) return false;
    int cx = (rc.left + rc.right) / 2;
    int cy = (rc.top + rc.bottom) / 2;
    LPARAM lp = MAKELPARAM(cx, cy);
    SendMessageW(tb, WM_LBUTTONDOWN, MK_LBUTTON, lp);
    Sleep(50);
    SendMessageW(tb, WM_LBUTTONUP, 0, lp);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolbar1165Btn0(LPVOID lpParam) { (void)lpParam; return ClickToolbar1165ButtonByIndex(0) ? 1 : 0; }
__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolbar1165Btn1(LPVOID lpParam) { (void)lpParam; return ClickToolbar1165ButtonByIndex(1) ? 1 : 0; }
__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolbar1165Btn2(LPVOID lpParam) { (void)lpParam; return ClickToolbar1165ButtonByIndex(2) ? 1 : 0; }

__declspec(dllexport) bool DumpToolbarButtons()
{
    HWND tb = FindToolAnchor();
    if (!tb) return false;

    FILE* f = NULL;
    _wfopen_s(&f, L"C:\\InSpecHookDLL_toolbar.txt", L"w, ccs=UTF-8");
    if (!f) return false;

    int count = (int)SendMessageW(tb, TB_BUTTONCOUNT, 0, 0);
    fwprintf(f, L"toolbar hwnd=0x%p buttonCount=%d\n", tb, count);

    for (int i = 0; i < count; i++)
    {
        TBBUTTON btn = {0};
        SendMessageW(tb, TB_GETBUTTON, (WPARAM)i, (LPARAM)&btn);
        RECT rc = {0};
        SendMessageW(tb, TB_GETITEMRECT, (WPARAM)i, (LPARAM)&rc);
        wchar_t textBuf[128] = {0};
        LRESULT textLen = SendMessageW(tb, TB_GETBUTTONTEXTW, (WPARAM)btn.idCommand, (LPARAM)textBuf);
        fwprintf(f, L"[%d] idCommand=%d state=0x%02X style=0x%02X rect=(%ld,%ld,%ld,%ld) text=\"%s\" textLen=%ld\n",
            i, btn.idCommand, btn.fsState, btn.fsStyle, rc.left, rc.top, rc.right, rc.bottom, textBuf, textLen);
    }

    fclose(f);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpToolbarButtons(LPVOID lpParam)
{
    (void)lpParam;
    return DumpToolbarButtons() ? 1 : 0;
}

__declspec(dllexport) bool ClickToolButtonByIndex(int index)
{
    HWND tb = FindToolAnchor();
    if (!tb) return false;

    RECT rc = {0};
    if (!SendMessageW(tb, TB_GETITEMRECT, (WPARAM)index, (LPARAM)&rc)) return false;

    int cx = (rc.left + rc.right) / 2;
    int cy = (rc.top + rc.bottom) / 2;
    LPARAM lp = MAKELPARAM(cx, cy);

    SendMessageW(tb, WM_LBUTTONDOWN, MK_LBUTTON, lp);
    Sleep(50);
    SendMessageW(tb, WM_LBUTTONUP, 0, lp);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolButton0(LPVOID lpParam) { (void)lpParam; return ClickToolButtonByIndex(0) ? 1 : 0; }
__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolButton1(LPVOID lpParam) { (void)lpParam; return ClickToolButtonByIndex(1) ? 1 : 0; }
__declspec(dllexport) DWORD WINAPI ThreadWrapper_ClickToolButton2(LPVOID lpParam) { (void)lpParam; return ClickToolButtonByIndex(2) ? 1 : 0; }

// ============================================================
// 「即時讀寫」注入按鈕：Demo 用，展示 HookDLL 能把我方控制項直接
// 加進 InSpec 自己的視窗（不是外部應用程式疊加），點下去彈出我方
// 輸入框，寫值直接送進 InSpec 自己的提示流程，全程不需要外部程式
// 介入——跟既有 Demo 按鈕同一套監看執行緒＋獨立旗標模式，
// 沿用 FindToolAnchor 定位、AutoFillAndConfirmPrompt 寫值。
// CJK 字面值直接寫（build 用 /utf-8，本輪 FindWindowW(L"輸入提示") 已多次
// 實機驗證這樣寫沒問題），不比照舊註解提到的 \u 轉義。
// ============================================================
static const wchar_t* LIVE_EDIT_BTN_CLASS = L"InSpecLiveEditButton";
static const wchar_t* LIVE_EDIT_POPUP_CLASS = L"InSpecLiveEditPopup";
static HWND g_liveEditPopupEdit = NULL;

static bool WaitForPromptDialog(int maxTries, int delayMs)
{
    for (int i = 0; i < maxTries; i++)
    {
        if (FindWindowW(NULL, L"輸入提示")) return true; // "輸入提示"
        Sleep(delayMs);
    }
    return false;
}

static LRESULT CALLBACK LiveEditPopupProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_CREATE:
    {
        // "輸入要寫入 InSpec 的值:"
        CreateWindowExW(0, L"Static",
            L"輸入要寫入 InSpec 的值:",
            WS_CHILD | WS_VISIBLE, 10, 10, 260, 18, hWnd, NULL, g_hSelfModule, NULL);
        g_liveEditPopupEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"Edit", L"",
            WS_CHILD | WS_VISIBLE | WS_BORDER, 10, 32, 260, 24,
            hWnd, NULL, g_hSelfModule, NULL);
        // "寫入 InSpec"
        CreateWindowExW(0, L"Button", L"寫入 InSpec",
            WS_CHILD | WS_VISIBLE, 10, 66, 120, 28,
            hWnd, (HMENU)(INT_PTR)1, g_hSelfModule, NULL);
        SetFocus(g_liveEditPopupEdit);
        return 0;
    }
    case WM_COMMAND:
        if (LOWORD(wParam) == 1)
        {
            wchar_t buf[256] = {0};
            GetWindowTextW(g_liveEditPopupEdit, buf, 255);

            // "輸入提示" 對話框若尚未開啟，先觸發執行（同一顆執行鈕，id=1165 index 0）
            if (!FindWindowW(NULL, L"輸入提示"))
            {
                ClickToolbar1165ButtonByIndex(0);
                WaitForPromptDialog(20, 100);
            }
            AutoFillAndConfirmPrompt(buf);
            DestroyWindow(hWnd);
        }
        return 0;
    case WM_DESTROY:
        g_liveEditPopupEdit = NULL;
        return 0;
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

static LRESULT CALLBACK LiveEditButtonProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc; GetClientRect(hWnd, &rc);
        FillRect(hdc, &rc, GetSysColorBrush(COLOR_HIGHLIGHT));
        DrawEdge(hdc, &rc, EDGE_RAISED, BF_RECT);
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, RGB(255, 255, 255));
        // "★ 我方擴充輸入"
        DrawTextW(hdc, L"★ 我方擴充輸入", -1, &rc,
            DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        EndPaint(hWnd, &ps);
        return 0;
    }
    case WM_LBUTTONUP:
    {
        // "★ 我方擴充功能"
        HWND popup = CreateWindowExW(WS_EX_TOPMOST, LIVE_EDIT_POPUP_CLASS,
            L"★ 我方擴充功能",
            WS_POPUP | WS_CAPTION | WS_SYSMENU, CW_USEDEFAULT, CW_USEDEFAULT, 300, 140,
            NULL, NULL, g_hSelfModule, NULL);
        if (popup) ShowWindow(popup, SW_SHOW);
        return 0;
    }
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

static bool IsMeasurementComplete(HWND dlg)
{
    if (!dlg) return false;
    HWND doneMarker = GetDlgItem(dlg, SUMMARY_DONE_ID);
    return doneMarker && IsWindowVisible(doneMarker);
}

static LRESULT CALLBACK ReloadButtonProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
static LRESULT CALLBACK ReloadMaskProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
static void EnsureMonitorThread();

static void RegisterMonitorClasses()
{
    if (g_classesRegistered) return;
    WNDCLASSEXW wc = {0};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = SummaryOverlayProc;
    wc.hInstance = g_hSelfModule;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.lpszClassName = SUMMARY_OVERLAY_CLASS;
    RegisterClassExW(&wc);

    WNDCLASSEXW wb = {0};
    wb.cbSize = sizeof(wb);
    wb.lpfnWndProc = DemoButtonProc;
    wb.hInstance = g_hSelfModule;
    wb.hCursor = LoadCursor(NULL, IDC_HAND);
    wb.lpszClassName = DEMO_BTN_CLASS;
    RegisterClassExW(&wb);

    WNDCLASSEXW wl = {0};
    wl.cbSize = sizeof(wl);
    wl.lpfnWndProc = LiveEditButtonProc;
    wl.hInstance = g_hSelfModule;
    wl.hCursor = LoadCursor(NULL, IDC_HAND);
    wl.lpszClassName = LIVE_EDIT_BTN_CLASS;
    RegisterClassExW(&wl);

    WNDCLASSEXW wp = {0};
    wp.cbSize = sizeof(wp);
    wp.lpfnWndProc = LiveEditPopupProc;
    wp.hInstance = g_hSelfModule;
    wp.hCursor = LoadCursor(NULL, IDC_ARROW);
    wp.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
    wp.lpszClassName = LIVE_EDIT_POPUP_CLASS;
    RegisterClassExW(&wp);

    WNDCLASSEXW wr = {0};
    wr.cbSize = sizeof(wr);
    wr.lpfnWndProc = ReloadButtonProc;
    wr.hInstance = g_hSelfModule;
    wr.hCursor = LoadCursor(NULL, IDC_HAND);
    wr.lpszClassName = RELOAD_BTN_CLASS;
    RegisterClassExW(&wr);

    WNDCLASSEXW wm = {0};
    wm.cbSize = sizeof(wm);
    wm.lpfnWndProc = ReloadMaskProc;
    wm.hInstance = g_hSelfModule;
    wm.hCursor = LoadCursor(NULL, IDC_ARROW);
    wm.lpszClassName = RELOAD_MASK_CLASS;
    RegisterClassExW(&wm);

    g_classesRegistered = true;
}

// 每個 tick：維護摘要打勾覆蓋層——只蓋住完成指示小圖示（SUMMARY_DONE_ID）本身的範圍，
// 不蓋住整個摘要面板。摘要面板本身不可編輯，只換完成小圖示的樣式（打勾/叉叉/顏色/文字），
// 定案（2026-07-30）：先前版本嘗試把「輸入值」那一行也換成可編輯欄位，使用者已明確撤回這個方向，
// 可編輯能力改放到 X/Y/Z 座標區（見 TickXyzEdit），摘要面板維持「只讀＋樣式可換」。
static void TickOverlay()
{
    if (!g_overlayEnabled)
    {
        if (g_summaryOverlay && IsWindow(g_summaryOverlay))
            ShowWindow(g_summaryOverlay, SW_HIDE);
        return;
    }
    if (!g_summaryDlg || !IsWindow(g_summaryDlg))
    {
        g_summaryDlg = FindSummaryDialog();
        g_summaryOverlay = NULL;
    }
    if (!g_summaryDlg) return;

    HWND doneMarker = GetDlgItem(g_summaryDlg, SUMMARY_DONE_ID);
    if (!doneMarker) return;
    RECT markerRc; GetWindowRect(doneMarker, &markerRc);
    POINT tl = { markerRc.left, markerRc.top };
    ScreenToClient(g_summaryDlg, &tl);
    int mw = markerRc.right - markerRc.left;
    int mh = markerRc.bottom - markerRc.top;
    if (mw <= 0 || mh <= 0) return;

    if (!g_summaryOverlay || !IsWindow(g_summaryOverlay))
    {
        g_summaryOverlay = CreateWindowExW(0, SUMMARY_OVERLAY_CLASS, L"",
            WS_CHILD | WS_CLIPSIBLINGS, tl.x, tl.y, mw, mh,
            g_summaryDlg, NULL, g_hSelfModule, NULL);
    }
    else
    {
        SetWindowPos(g_summaryOverlay, HWND_TOP, tl.x, tl.y, mw, mh, SWP_NOACTIVATE);
    }
    if (!g_summaryOverlay) return;

    if (IsMeasurementComplete(g_summaryDlg))
    {
        SetWindowPos(g_summaryOverlay, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        if (!IsWindowVisible(g_summaryOverlay))
            ShowWindow(g_summaryOverlay, SW_SHOWNA);
        InvalidateRect(g_summaryOverlay, NULL, FALSE);
    }
    else if (IsWindowVisible(g_summaryOverlay))
    {
        ShowWindow(g_summaryOverlay, SW_HIDE);
    }
}

// ============================================================
// 摘要「輸入值」就地編輯（2026-07-30 恢復——使用者最終驗收清單明確要求「輸入框可直接
// 調整摘要文字，不是自動填寫執行→彈出視窗」）：把 GDI 畫出來的數值行換成疊在同一位置的
// 真正 Win32 Edit 控制項。InSpec 沒有原地改值介面，改值等於重新回答一次輸入提示，離開
// 焦點或按 Enter 時沿用「觸發執行→填值→確認」那條已驗證過的鏈寫回。座標疊在 GXWND
// （摘要對話框內部，非頂層 InSpecAppFrame 直接子視窗）——這個巢狀深度實測過會正常顯示，
// 跟 X/Y/Z／重新載入蒙版疊在 InSpecAppFrame 直接子視窗會「有功能但畫面不出現」不同。
// ============================================================
static const int SUMMARY_VALUE_X = 43;  // 實機查出真實文字起點 x=45，這裡留 2px 安全邊界
static const int SUMMARY_VALUE_Y = 52;
static const int SUMMARY_VALUE_W = 240;
static const int SUMMARY_VALUE_H = 18;

static WNDPROC g_origValueEditProc = NULL;
static bool g_valueEditCommitting = false;

static void CommitValueEdit(HWND hEdit)
{
    if (g_valueEditCommitting) return;
    g_valueEditCommitting = true;

    wchar_t buf[128] = {0};
    GetWindowTextW(hEdit, buf, 127);

    if (!FindWindowW(NULL, L"輸入提示"))
    {
        ClickToolbar1165ButtonByIndex(0);
        for (int i = 0; i < 20 && !FindWindowW(NULL, L"輸入提示"); i++) Sleep(100);
    }
    AutoFillAndConfirmPrompt(buf);

    g_valueEditCommitting = false;
}

static LRESULT CALLBACK ValueEditSubclassProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_KILLFOCUS:
        CommitValueEdit(hWnd);
        break;
    case WM_KEYDOWN:
        if (wParam == VK_RETURN)
        {
            CommitValueEdit(hWnd);
            return 0;
        }
        break;
    default:
        break;
    }
    return CallWindowProcW(g_origValueEditProc, hWnd, msg, wParam, lParam);
}

// 每個 tick：維護「輸入值」就地編輯欄位，跟 TickOverlay 共用 g_overlayEnabled 開關。
static void TickValueEdit()
{
    if (!g_overlayEnabled)
    {
        if (g_valueEdit && IsWindow(g_valueEdit)) { DestroyWindow(g_valueEdit); g_valueEdit = NULL; }
        g_summaryGridCached = NULL;
        return;
    }

    if (!g_gdiHooksInstalled) InstallGdiCaptureHooks();

    HWND grid = FindSummaryGrid();
    g_summaryGridCached = grid;
    if (!grid) return;

    if (!g_valueEdit || !IsWindow(g_valueEdit))
    {
        g_valueEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"Edit", L"",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
            SUMMARY_VALUE_X, SUMMARY_VALUE_Y, SUMMARY_VALUE_W, SUMMARY_VALUE_H,
            grid, NULL, g_hSelfModule, NULL);
        if (g_valueEdit)
        {
            g_origValueEditProc = (WNDPROC)SetWindowLongPtrW(g_valueEdit, GWLP_WNDPROC, (LONG_PTR)ValueEditSubclassProc);
            SendMessageW(g_valueEdit, WM_SETFONT, (WPARAM)GetStockObject(DEFAULT_GUI_FONT), TRUE);
        }
    }
    else
    {
        SetWindowPos(g_valueEdit, HWND_TOP, SUMMARY_VALUE_X, SUMMARY_VALUE_Y, SUMMARY_VALUE_W, SUMMARY_VALUE_H,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
    if (!g_valueEdit) return;

    if (GetFocus() != g_valueEdit)
    {
        wchar_t current[128] = {0};
        if (g_csValueTextInited)
        {
            EnterCriticalSection(&g_csValueText);
            wcsncpy_s(current, 128, g_lastValueText, _TRUNCATE);
            LeaveCriticalSection(&g_csValueText);
        }
        wchar_t existing[128] = {0};
        GetWindowTextW(g_valueEdit, existing, 127);
        if (wcscmp(current, existing) != 0)
            SetWindowTextW(g_valueEdit, current);
    }
}

// ============================================================
// X/Y/Z 座標就地編輯：跟摘要面板不同，這裡是 InSpec 自己的標準 Win32 Static
// 控制項（id 19000/19001/19002，DumpAllChildren 查到的真實值，不是猜測），
// 不是 owner-drawn GDI 文字——不需要 hook TextOutW 去猜座標，直接
// GetWindowTextW 讀真實內容、GetWindowRect 讀真實位置，疊一個 Edit 控制項
// 蓋住同一塊區域即可，不會有摘要面板那種「兩層文字疊影」問題（沒有其他人
// 會在同一塊像素上重繪，Static 只在自己的 WM_PAINT 裡畫自己，不會畫到子視窗上）。
// ============================================================
static const int XYZ_STATIC_ID[3] = { 19000, 19001, 19002 };
static HWND g_xyzStatic[3] = { NULL, NULL, NULL };
static WNDPROC g_origXyzEditProc[3] = { NULL, NULL, NULL };

static BOOL CALLBACK FindXyzStaticProc(HWND hWnd, LPARAM lParam)
{
    (void)lParam;
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"Static") != 0) return TRUE;
    int id = GetDlgCtrlID(hWnd);
    for (int i = 0; i < 3; i++)
        if (id == XYZ_STATIC_ID[i]) g_xyzStatic[i] = hWnd;
    return TRUE;
}

static void FindXyzStatics()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return;
    g_xyzStatic[0] = g_xyzStatic[1] = g_xyzStatic[2] = NULL;
    EnumChildWindows(main, FindXyzStaticProc, 0);
}

// InSpec 沒有「原地改座標」的介面可呼叫，這裡改值的定義就是：離開焦點或按 Enter 時，
// 把使用者輸入的新文字直接寫回對應的真實 Static 控制項（SetWindowTextW）——這是
// InSpec 自己顯示的座標值真的變成新內容，不是我方另外疊一層假數字，跟摘要面板小
// 圖示的「換掉 InSpec 前端顯示」是同一種手法，只是這裡改的是文字內容而非圖形。
static LRESULT CALLBACK XyzEditSubclassProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    int idx = -1;
    for (int i = 0; i < 3; i++) if (g_xyzEdit[i] == hWnd) { idx = i; break; }

    if (idx >= 0 && (msg == WM_KILLFOCUS || (msg == WM_KEYDOWN && wParam == VK_RETURN)))
    {
        wchar_t buf[64] = {0};
        GetWindowTextW(hWnd, buf, 63);
        if (g_xyzStatic[idx] && IsWindow(g_xyzStatic[idx]))
            SetWindowTextW(g_xyzStatic[idx], buf);
        if (msg == WM_KEYDOWN) return 0; // 吃掉 Enter，避免系統警示音
    }

    if (idx < 0) return DefWindowProcW(hWnd, msg, wParam, lParam);
    return CallWindowProcW(g_origXyzEditProc[idx], hWnd, msg, wParam, lParam);
}

// 每個 tick：維護 X/Y/Z 三個就地編輯欄位
static void TickXyzEdit()
{
    if (!g_xyzEditEnabled)
    {
        for (int i = 0; i < 3; i++)
            if (g_xyzEdit[i] && IsWindow(g_xyzEdit[i])) { DestroyWindow(g_xyzEdit[i]); g_xyzEdit[i] = NULL; }
        return;
    }

    bool needFind = false;
    for (int i = 0; i < 3; i++)
        if (!g_xyzStatic[i] || !IsWindow(g_xyzStatic[i])) needFind = true;
    if (needFind) FindXyzStatics();

    for (int i = 0; i < 3; i++)
    {
        if (!g_xyzStatic[i] || !IsWindow(g_xyzStatic[i])) continue;

        // 用 Static 的真實直接父視窗當我方 Edit 的父視窗（不是頂層 InSpecAppFrame）——
        // 這是實機驗證過的教訓：疊在 InSpecAppFrame 直接子視窗底下的覆蓋層，結構/功能都對
        // 但畫面上完全看不到（推斷是 InSpec 自己的即時讀值重繪機制蓋掉了像素）；摘要面板
        // 的打勾覆蓋層／就地編輯欄位疊在較深層的巢狀視窗（GXWND／摘要對話框）底下就能正常
        // 顯示，這裡改成同樣的巢狀父視窗策略。
        HWND parent = GetParent(g_xyzStatic[i]);
        if (!parent) continue;

        RECT rc; GetWindowRect(g_xyzStatic[i], &rc);
        POINT tl = { rc.left, rc.top };
        POINT br = { rc.right, rc.bottom };
        ScreenToClient(parent, &tl);
        ScreenToClient(parent, &br);
        int w = br.x - tl.x, h = br.y - tl.y;
        if (w <= 0 || h <= 0) continue;

        if (!g_xyzEdit[i] || !IsWindow(g_xyzEdit[i]))
        {
            g_xyzEdit[i] = CreateWindowExW(WS_EX_CLIENTEDGE, L"Edit", L"",
                WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL | ES_RIGHT,
                tl.x, tl.y, w, h, parent, NULL, g_hSelfModule, NULL);
            if (g_xyzEdit[i])
            {
                g_origXyzEditProc[i] = (WNDPROC)SetWindowLongPtrW(g_xyzEdit[i], GWLP_WNDPROC, (LONG_PTR)XyzEditSubclassProc);
                SendMessageW(g_xyzEdit[i], WM_SETFONT, (WPARAM)GetStockObject(DEFAULT_GUI_FONT), TRUE);
            }
        }
        else
        {
            SetWindowPos(g_xyzEdit[i], HWND_TOP, tl.x, tl.y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        if (!g_xyzEdit[i]) continue;

        // 每個 tick 都強制重繪一次——實測發現 InSpec 自己的即時座標讀值區塊會頻繁自畫，
        // 偶爾把我方覆蓋層的像素蓋掉（結構/Z-order 仍然正確，只是畫面被蓋），
        // 固定頻率補畫可以縮短「被蓋住看不到」的時間窗，不是根本修法但立即有效。
        InvalidateRect(g_xyzEdit[i], NULL, TRUE);

        // 使用者正在打字時不要覆蓋掉；沒有 focus 時直接讀真實 Static 內容同步顯示，
        // 不需要任何 GDI hook 或快取，因為這是真的視窗、真的能直接讀。
        if (GetFocus() != g_xyzEdit[i])
        {
            wchar_t current[64] = {0};
            GetWindowTextW(g_xyzStatic[i], current, 63);
            wchar_t existing[64] = {0};
            GetWindowTextW(g_xyzEdit[i], existing, 63);
            if (wcscmp(current, existing) != 0)
                SetWindowTextW(g_xyzEdit[i], current);
        }
    }
}

// ============================================================
// 重新載入蒙版：蓋住整個 InSpec 主視窗，中間顯示「重新載入中」文字＋三個左右
// 抖動的點，同時觸發真實的 InSpec 動作（元素→重新量測，選單 id=32920，
// DumpMainMenu 查到的真實值）——不是假動畫掛一個沒發生的事，是真的觸發
// InSpec 自己的重新量測流程，蒙版只是把這個過程包裝成有展示效果的載入畫面。
// 動畫至少播滿 2 秒（展示效果要求），不是等真實動作完成才收，因為真實動作
// 可能幾百毫秒就做完，播不滿使用者看不到效果。
// ============================================================
static const UINT RELOAD_ANIM_TIMER_ID = 9001;
static const DWORD RELOAD_DURATION_MS = 2000;
static DWORD g_reloadStartTick = 0;

static void DrawReloadMask(HDC hdc, const RECT& rc)
{
    HBRUSH bg = CreateSolidBrush(RGB(250, 250, 250));
    FillRect(hdc, &rc, bg);
    DeleteObject(bg);

    int cx = (rc.left + rc.right) / 2;
    int cy = (rc.top + rc.bottom) / 2;

    DWORD elapsed = GetTickCount() - g_reloadStartTick;
    double phase = (double)(elapsed % 900) / 900.0;               // 0..1 週期
    double wave = phase < 0.5 ? (phase * 2.0) : (2.0 - phase * 2.0); // 三角波：來回
    int swing = 30;
    int offset = (int)((wave - 0.5) * 2.0 * swing);

    HFONT font = CreateFontW(-24, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
        DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    HGDIOBJ oldFont = SelectObject(hdc, font);
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, RGB(60, 60, 60));
    RECT textRc = { rc.left, cy - 60, rc.right, cy - 20 };
    DrawTextW(hdc, L"重新載入中", -1, &textRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    SelectObject(hdc, oldFont);
    DeleteObject(font);

    int dotR = 8, gap = 26;
    HBRUSH dotBrush = CreateSolidBrush(RGB(70, 130, 220));
    HGDIOBJ oldBrush = SelectObject(hdc, dotBrush);
    HGDIOBJ oldPen = SelectObject(hdc, GetStockObject(NULL_PEN));
    for (int i = -1; i <= 1; i++)
    {
        int dx = cx + i * gap + offset;
        Ellipse(hdc, dx - dotR, cy - dotR, dx + dotR, cy + dotR);
    }
    SelectObject(hdc, oldBrush);
    SelectObject(hdc, oldPen);
    DeleteObject(dotBrush);
}

static LRESULT CALLBACK ReloadMaskProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc; GetClientRect(hWnd, &rc);
        DrawReloadMask(hdc, rc);
        EndPaint(hWnd, &ps);
        return 0;
    }
    case WM_TIMER:
        if (wParam == RELOAD_ANIM_TIMER_ID)
        {
            DWORD elapsed = GetTickCount() - g_reloadStartTick;
            if (elapsed >= RELOAD_DURATION_MS)
            {
                KillTimer(hWnd, RELOAD_ANIM_TIMER_ID);
                ShowWindow(hWnd, SW_HIDE);
                g_reloadMaskActive = false;
            }
            else
            {
                InvalidateRect(hWnd, NULL, FALSE);
            }
            return 0;
        }
        break;
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

// 目標句（2026-07-30 實機測試發現：蒙版建了但螢幕上完全看不到）：CreateWindowExW／SetTimer
// 建的視窗歸屬於「呼叫當下的執行緒」，但 TriggerReload 是透過 CreateRemoteThread 建立的
// 一次性執行緒呼叫的匯出函式——那條執行緒一執行完這個函式就結束，Windows 會自動銷毀該執行緒
// 名下所有視窗，蒙版於是剛建好就被系統收掉，畫面上完全看不到。摘要打勾／XYZ 欄位之所以沒有
// 這個問題，是因為它們的 CreateWindowExW 都寫在 MonitorThread 自己的 Tick* 函式裡執行，
// MonitorThread 是長駐執行緒，視窗才活得下去。修法：TriggerReload 只設旗標，真正的
// CreateWindowExW／SetTimer 移到 TickReloadMask（跟其他 Tick* 一樣，在 MonitorThread 內執行）。
__declspec(dllexport) bool TriggerReload()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;

    RegisterMonitorClasses();
    EnsureMonitorThread();

    g_reloadStartTick = GetTickCount();
    g_reloadMaskActive = true;

    // 真實動作：元素→重新量測（選單 id=32920）。
    SendMessageW(main, WM_COMMAND, MAKEWPARAM(32920, 0), 0);

    return true;
}

// 找 InSpecAppFrame 底下最大的 AfxMDIFrame100u（實測是內容區域本身，跟直接疊在
// InSpecAppFrame 上不同層——直接疊在 InSpecAppFrame 上結構/功能正確但畫面不出現，
// 疊在這一層底下比照摘要面板 GXWND 巢狀成功的模式）。
static HWND g_mdiContentWnd = NULL;
static BOOL CALLBACK FindMdiContentProc(HWND hWnd, LPARAM lParam)
{
    wchar_t cls[64] = {0};
    GetClassNameW(hWnd, cls, 63);
    if (wcscmp(cls, L"AfxMDIFrame100u") == 0)
    {
        RECT rc; GetWindowRect(hWnd, &rc);
        RECT best = {0,0,0,0};
        if (g_mdiContentWnd) GetWindowRect(g_mdiContentWnd, &best);
        long area = (long)(rc.right - rc.left) * (rc.bottom - rc.top);
        long bestArea = (long)(best.right - best.left) * (best.bottom - best.top);
        if (area > bestArea) g_mdiContentWnd = hWnd;
    }
    (void)lParam;
    return TRUE;
}
static HWND FindMdiContentWindow()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return NULL;
    g_mdiContentWnd = NULL;
    EnumChildWindows(main, FindMdiContentProc, 0);
    return g_mdiContentWnd;
}

// 每個 tick：維護重新載入蒙版視窗——在 MonitorThread 自己的執行緒內建立/顯示，
// 不在 TriggerReload 那條一次性執行緒內建（理由見上方 TriggerReload 的說明）。
static void TickReloadMask()
{
    if (!g_reloadMaskActive)
    {
        if (g_reloadMaskWnd && IsWindow(g_reloadMaskWnd)) ShowWindow(g_reloadMaskWnd, SW_HIDE);
        return;
    }

    HWND parent = FindMdiContentWindow();
    if (!parent) { g_reloadMaskActive = false; return; }

    RECT rc; GetClientRect(parent, &rc);
    bool justCreated = false;
    if (!g_reloadMaskWnd || !IsWindow(g_reloadMaskWnd))
    {
        g_reloadMaskWnd = CreateWindowExW(0, RELOAD_MASK_CLASS, L"",
            WS_CHILD | WS_CLIPSIBLINGS, 0, 0, rc.right, rc.bottom,
            parent, NULL, g_hSelfModule, NULL);
        justCreated = true;
    }
    if (!g_reloadMaskWnd) { g_reloadMaskActive = false; return; }

    SetWindowPos(g_reloadMaskWnd, HWND_TOP, 0, 0, rc.right, rc.bottom, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    if (justCreated)
        SetTimer(g_reloadMaskWnd, RELOAD_ANIM_TIMER_ID, 50, NULL);
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_TriggerReload(LPVOID lpParam)
{
    (void)lpParam;
    return TriggerReload() ? 1 : 0;
}

static LRESULT CALLBACK ReloadButtonProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc; GetClientRect(hWnd, &rc);
        FillRect(hdc, &rc, GetSysColorBrush(COLOR_BTNFACE));
        DrawEdge(hdc, &rc, EDGE_RAISED, BF_RECT);
        SetBkMode(hdc, TRANSPARENT);
        DrawTextW(hdc, L"重新載入", -1, &rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        EndPaint(hWnd, &ps);
        return 0;
    }
    case WM_LBUTTONUP:
        TriggerReload();
        return 0;
    default:
        break;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

// 每個 tick：維護重新載入按鈕（放在 Demo/LiveEdit 按鈕右側，同一列）
static void TickReloadButton()
{
    if (!g_reloadBtnEnabled)
    {
        if (g_reloadBtn && IsWindow(g_reloadBtn)) { DestroyWindow(g_reloadBtn); g_reloadBtn = NULL; }
        return;
    }
    HWND anchor = FindToolAnchor();
    if (!anchor) return;
    HWND parent = GetParent(anchor);
    if (!parent) return;

    RECT tr; GetWindowRect(anchor, &tr);
    POINT tl = { tr.left + 336, tr.bottom + 20 }; // Demo(148) + 間距10 + LiveEdit(168) + 間距10 右側
    ScreenToClient(parent, &tl);
    const int bw = 120, bh = 22;

    if (!g_reloadBtn || !IsWindow(g_reloadBtn))
    {
        g_reloadBtn = CreateWindowExW(0, RELOAD_BTN_CLASS, L"",
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
            tl.x, tl.y, bw, bh, parent, NULL, g_hSelfModule, NULL);
    }
    else
    {
        SetWindowPos(g_reloadBtn, HWND_TOP, tl.x, tl.y, bw, bh, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}

// 每個 tick：維護 demo 按鈕（放在「元素」右側工具列下方）
static void TickDemoButton()
{
    if (!g_demoEnabled)
    {
        if (g_demoBtn && IsWindow(g_demoBtn)) { DestroyWindow(g_demoBtn); g_demoBtn = NULL; }
        return;
    }
    HWND anchor = FindToolAnchor();
    if (!anchor) return;
    HWND parent = GetParent(anchor);
    if (!parent) return;

    RECT tr; GetWindowRect(anchor, &tr);       // 錨點工具列(螢幕座標)
    POINT tl = { tr.left, tr.bottom + 20 };     // 放在工具列群下方
    ScreenToClient(parent, &tl);
    const int bw = 148, bh = 22;

    if (!g_demoBtn || !IsWindow(g_demoBtn))
    {
        g_demoBtn = CreateWindowExW(0, DEMO_BTN_CLASS, L"Demo",
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
            tl.x, tl.y, bw, bh, parent, NULL, g_hSelfModule, NULL);
    }
    else
    {
        SetWindowPos(g_demoBtn, HWND_TOP, tl.x, tl.y, bw, bh, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}

// 每個 tick：維護即時讀寫注入按鈕（放在 Demo 按鈕右側，同一列）
static void TickLiveEditButton()
{
    if (!g_liveEditEnabled)
    {
        if (g_liveEditBtn && IsWindow(g_liveEditBtn)) { DestroyWindow(g_liveEditBtn); g_liveEditBtn = NULL; }
        return;
    }
    HWND anchor = FindToolAnchor();
    if (!anchor) return;
    HWND parent = GetParent(anchor);
    if (!parent) return;

    RECT tr; GetWindowRect(anchor, &tr);
    POINT tl = { tr.left + 158, tr.bottom + 20 };  // Demo 按鈕（寬 148 + 間距 10）右側
    ScreenToClient(parent, &tl);
    const int bw = 168, bh = 22;

    if (!g_liveEditBtn || !IsWindow(g_liveEditBtn))
    {
        g_liveEditBtn = CreateWindowExW(0, LIVE_EDIT_BTN_CLASS, L"",
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
            tl.x, tl.y, bw, bh, parent, NULL, g_hSelfModule, NULL);
    }
    else
    {
        SetWindowPos(g_liveEditBtn, HWND_TOP, tl.x, tl.y, bw, bh, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}

static DWORD WINAPI MonitorThread(LPVOID lpParam)
{
    (void)lpParam;
    RegisterMonitorClasses();

    // hWnd=NULL 時 SetTimer 回傳系統自配 ID，WM_TIMER 的 wParam 是它，必須用回傳值比對
    UINT_PTR timerId = SetTimer(NULL, 0, SUMMARY_POLL_MS, NULL);

    MSG msg;
    while (g_monitorRunning && GetMessage(&msg, NULL, 0, 0) > 0)
    {
        if (msg.message == WM_TIMER && msg.wParam == timerId)
        {
            TickOverlay();
            TickValueEdit();
            TickXyzEdit();
            TickReloadButton();
            TickReloadMask();
            TickDemoButton();
            TickLiveEditButton();
            continue;
        }
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    KillTimer(NULL, timerId);
    if (g_summaryOverlay && IsWindow(g_summaryOverlay)) DestroyWindow(g_summaryOverlay);
    if (g_valueEdit && IsWindow(g_valueEdit)) DestroyWindow(g_valueEdit);
    for (int i = 0; i < 3; i++) { if (g_xyzEdit[i] && IsWindow(g_xyzEdit[i])) DestroyWindow(g_xyzEdit[i]); g_xyzEdit[i] = NULL; }
    if (g_reloadMaskWnd && IsWindow(g_reloadMaskWnd)) DestroyWindow(g_reloadMaskWnd);
    if (g_reloadBtn && IsWindow(g_reloadBtn)) DestroyWindow(g_reloadBtn);
    if (g_demoBtn && IsWindow(g_demoBtn)) DestroyWindow(g_demoBtn);
    if (g_liveEditBtn && IsWindow(g_liveEditBtn)) DestroyWindow(g_liveEditBtn);
    g_summaryOverlay = NULL;
    g_valueEdit = NULL;
    g_summaryGridCached = NULL;
    g_reloadMaskWnd = NULL;
    g_reloadBtn = NULL;
    g_summaryDlg = NULL;
    g_demoBtn = NULL;
    g_liveEditBtn = NULL;
    return 0;
}

static void EnsureMonitorThread()
{
    if (g_monitorRunning) return;
    g_monitorRunning = true;
    g_hMonitorThread = CreateThread(NULL, 0, MonitorThread, NULL, 0, &g_monitorThreadId);
    if (!g_hMonitorThread) g_monitorRunning = false;
}

static void StopMonitorIfIdle()
{
    if (g_overlayEnabled || g_demoEnabled || g_liveEditEnabled || g_xyzEditEnabled
        || g_reloadBtnEnabled || g_reloadMaskActive) return;  // 還有功能在用，保留執行緒
    if (!g_monitorRunning) return;
    g_monitorRunning = false;
    if (g_monitorThreadId) PostThreadMessage(g_monitorThreadId, WM_NULL, 0, 0);
    if (g_hMonitorThread)
    {
        WaitForSingleObject(g_hMonitorThread, 2000);
        CloseHandle(g_hMonitorThread);
        g_hMonitorThread = NULL;
        g_monitorThreadId = 0;
    }
}

extern "C" {

__declspec(dllexport) bool EnableApiHook()
{
    if (g_isApiHookEnabled) return true;

    InitializeCriticalSection(&g_csLog);

    if (!g_hLogEvent)
    {
        g_hLogEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
    }

    if (g_hLogEvent && !g_hLogThread)
    {
        g_isLogThreadRunning = true;
        DWORD threadId = 0;
        g_hLogThread = CreateThread(NULL, 0, LogThreadProc, NULL, 0, &threadId);
    }

    g_isApiHookEnabled = true;
    InstallAllHooks();
    WriteApiLog(L"[API] API Hook \u5DF2\u555F\u7528");
    return true;
}

__declspec(dllexport) bool DisableApiHook()
{
    if (!g_isApiHookEnabled) return true;

    g_isApiHookEnabled = false;
    RestoreAllIatPatches();

    if (g_hLogThread)
    {
        g_isLogThreadRunning = false;
        if (g_hLogEvent)
        {
            SetEvent(g_hLogEvent);
        }
        WaitForSingleObject(g_hLogThread, 2000);
        CloseHandle(g_hLogThread);
        g_hLogThread = NULL;
    }

    if (g_hLogEvent)
    {
        CloseHandle(g_hLogEvent);
        g_hLogEvent = NULL;
    }

    if (g_hLogPipe != INVALID_HANDLE_VALUE)
    {
        CloseHandle(g_hLogPipe);
        g_hLogPipe = INVALID_HANDLE_VALUE;
    }

    DeleteCriticalSection(&g_csLog);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableApiHook(LPVOID lpParam)
{
    (void)lpParam;
    return EnableApiHook() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableApiHook(LPVOID lpParam)
{
    (void)lpParam;
    return DisableApiHook() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_ExecuteScript(LPVOID lpParam)
{
    (void)lpParam;
    return 0;
}

__declspec(dllexport) bool ExecuteScriptCommand(const wchar_t* command)
{
    (void)command;
    return false;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_GetDllVersion(LPVOID lpParam)
{
    (void)lpParam;
    return 1;
}

struct VersionStringParams
{
    wchar_t* buffer;
    int bufferSize;
};

__declspec(dllexport) DWORD WINAPI ThreadWrapper_GetDllVersionString(LPVOID lpParam)
{
    VersionStringParams* p = (VersionStringParams*)lpParam;
    if (!p || !p->buffer || p->bufferSize <= 0)
    {
        return 0;
    }

    const wchar_t* version = L"InSpecHookDLL 1";
    wcsncpy_s(p->buffer, (size_t)p->bufferSize, version, _TRUNCATE);
    return 1;
}

__declspec(dllexport) LRESULT CALLBACK GetMsgProc(int code, WPARAM wParam, LPARAM lParam)
{
    (void)wParam;
    (void)lParam;
    return CallNextHookEx(NULL, code, wParam, lParam);
}

// ============================================================
// 診斷用（暫時）：列舉摘要對話框底下全部子控制項的 class/id/文字，
// 寫入固定路徑檔案供外部讀取。目的：找出摘要真實文字（例如
// "提示 / Machine / 輸入: / xxx"）實際承載在哪個子控制項，
// 找到答案後這段診斷程式碼應移除，不是正式功能的一部分。
// ============================================================
static FILE* g_dumpFile = NULL;
static int g_dumpDepth = 0;

static BOOL CALLBACK DumpChildProc(HWND hWnd, LPARAM lParam)
{
    wchar_t cls[64] = {0};
    wchar_t txt[512] = {0};
    GetClassNameW(hWnd, cls, 63);
    GetWindowTextW(hWnd, txt, 511);
    int id = GetDlgCtrlID(hWnd);
    if (g_dumpFile)
    {
        for (int i = 0; i < g_dumpDepth; i++) fwprintf(g_dumpFile, L"  ");
        fwprintf(g_dumpFile, L"hwnd=0x%p class=%s id=%d text=\"%s\"\n", hWnd, cls, id, txt);
    }
    g_dumpDepth++;
    EnumChildWindows(hWnd, DumpChildProc, (LPARAM)hWnd);
    g_dumpDepth--;
    return TRUE;
}

__declspec(dllexport) bool DumpSummaryControls()
{
    HWND dlg = FindSummaryDialog();
    _wfopen_s(&g_dumpFile, L"C:\\InSpecHookDLL_dump.txt", L"w, ccs=UTF-8");
    if (!g_dumpFile) return false;

    if (!dlg)
    {
        fwprintf(g_dumpFile, L"FindSummaryDialog() 找不到摘要對話框\n");
        fclose(g_dumpFile);
        g_dumpFile = NULL;
        return false;
    }

    wchar_t dlgCls[64] = {0};
    GetClassNameW(dlg, dlgCls, 63);
    fwprintf(g_dumpFile, L"摘要對話框 hwnd=0x%p class=%s\n", dlg, dlgCls);
    g_dumpDepth = 1;
    EnumChildWindows(dlg, DumpChildProc, (LPARAM)dlg);

    fclose(g_dumpFile);
    g_dumpFile = NULL;
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpSummaryControls(LPVOID lpParam)
{
    (void)lpParam;
    return DumpSummaryControls() ? 1 : 0;
}

__declspec(dllexport) bool EnableSummaryPatch()
{
    g_overlayEnabled = true;
    EnsureMonitorThread();
    return g_monitorRunning;
}

__declspec(dllexport) bool DisableSummaryPatch()
{
    g_overlayEnabled = false;
    StopMonitorIfIdle();
    return true;
}

// 覆蓋層樣式切換：展示「換掉 InSpec 前端顯示」的能力，mode/color 走整數直傳
// lpParameter（不需配置記憶體），自訂文字走既有的字串傳遞慣例（見 AutoFillAndConfirmPrompt）。
__declspec(dllexport) bool SetOverlaySymbolMode(int mode)
{
    if (mode < OverlaySymbol_Check || mode > OverlaySymbol_CustomText) return false;
    g_overlaySymbolMode = mode;
    if (g_summaryOverlay && IsWindow(g_summaryOverlay)) InvalidateRect(g_summaryOverlay, NULL, FALSE);
    return true;
}

__declspec(dllexport) bool SetOverlayColor(int rgbValue)
{
    g_overlaySymbolColor = (COLORREF)rgbValue;
    if (g_summaryOverlay && IsWindow(g_summaryOverlay)) InvalidateRect(g_summaryOverlay, NULL, FALSE);
    return true;
}

__declspec(dllexport) bool SetOverlayCustomText(const wchar_t* text)
{
    if (!text) return false;
    wcsncpy_s(g_overlayCustomText, 64, text, _TRUNCATE);
    if (g_summaryOverlay && IsWindow(g_summaryOverlay)) InvalidateRect(g_summaryOverlay, NULL, FALSE);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_SetOverlaySymbolMode(LPVOID lpParam)
{
    return SetOverlaySymbolMode((int)(INT_PTR)lpParam) ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_SetOverlayColor(LPVOID lpParam)
{
    return SetOverlayColor((int)(INT_PTR)lpParam) ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_SetOverlayCustomText(LPVOID lpParam)
{
    return SetOverlayCustomText((const wchar_t*)lpParam) ? 1 : 0;
}

__declspec(dllexport) bool EnableDemoButton()
{
    g_demoEnabled = true;
    EnsureMonitorThread();
    return g_monitorRunning;
}

__declspec(dllexport) bool DisableDemoButton()
{
    g_demoEnabled = false;
    StopMonitorIfIdle();
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableSummaryPatch(LPVOID lpParam)
{
    (void)lpParam;
    return EnableSummaryPatch() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableSummaryPatch(LPVOID lpParam)
{
    (void)lpParam;
    return DisableSummaryPatch() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableDemoButton(LPVOID lpParam)
{
    (void)lpParam;
    return EnableDemoButton() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableDemoButton(LPVOID lpParam)
{
    (void)lpParam;
    return DisableDemoButton() ? 1 : 0;
}

__declspec(dllexport) bool EnableLiveEditButton()
{
    g_liveEditEnabled = true;
    EnsureMonitorThread();
    return g_monitorRunning;
}

__declspec(dllexport) bool DisableLiveEditButton()
{
    g_liveEditEnabled = false;
    StopMonitorIfIdle();
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableLiveEditButton(LPVOID lpParam)
{
    (void)lpParam;
    return EnableLiveEditButton() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableLiveEditButton(LPVOID lpParam)
{
    (void)lpParam;
    return DisableLiveEditButton() ? 1 : 0;
}

__declspec(dllexport) bool EnableXyzEdit()
{
    g_xyzEditEnabled = true;
    EnsureMonitorThread();
    return g_monitorRunning;
}

__declspec(dllexport) bool DisableXyzEdit()
{
    g_xyzEditEnabled = false;
    StopMonitorIfIdle();
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableXyzEdit(LPVOID lpParam)
{
    (void)lpParam;
    return EnableXyzEdit() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableXyzEdit(LPVOID lpParam)
{
    (void)lpParam;
    return DisableXyzEdit() ? 1 : 0;
}

__declspec(dllexport) bool EnableReloadButton()
{
    g_reloadBtnEnabled = true;
    EnsureMonitorThread();
    return g_monitorRunning;
}

__declspec(dllexport) bool DisableReloadButton()
{
    g_reloadBtnEnabled = false;
    StopMonitorIfIdle();
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_EnableReloadButton(LPVOID lpParam)
{
    (void)lpParam;
    return EnableReloadButton() ? 1 : 0;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DisableReloadButton(LPVOID lpParam)
{
    (void)lpParam;
    return DisableReloadButton() ? 1 : 0;
}

// B 模式測試用：隱藏／顯示 InSpec 主視窗，驗證訊息注入在視窗隱藏後是否仍能送達
__declspec(dllexport) bool HideMainWindow()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;
    ShowWindow(main, SW_HIDE);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_HideMainWindow(LPVOID lpParam)
{
    (void)lpParam;
    return HideMainWindow() ? 1 : 0;
}

__declspec(dllexport) bool ShowMainWindow()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;
    ShowWindow(main, SW_SHOW);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_ShowMainWindow(LPVOID lpParam)
{
    (void)lpParam;
    return ShowMainWindow() ? 1 : 0;
}

// 診斷用：列出主視窗選單結構（頂層＋各頂層項目的子選單文字＋command id），
// 用來找「檔案(F)」選單裡「開啟」項目的真實 id，不用憑位置用猜的。
__declspec(dllexport) bool DumpMainMenu()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;
    HMENU menu = GetMenu(main);
    if (!menu) return false;

    FILE* f = NULL;
    _wfopen_s(&f, L"C:\\InSpecHookDLL_menu.txt", L"w, ccs=UTF-8");
    if (!f) return false;

    int topCount = GetMenuItemCount(menu);
    for (int i = 0; i < topCount; i++)
    {
        wchar_t topText[128] = {0};
        GetMenuStringW(menu, i, topText, 127, MF_BYPOSITION);
        fwprintf(f, L"[top %d] \"%s\"\n", i, topText);

        HMENU sub = GetSubMenu(menu, i);
        if (!sub) continue;
        int subCount = GetMenuItemCount(sub);
        for (int j = 0; j < subCount; j++)
        {
            wchar_t subText[128] = {0};
            GetMenuStringW(sub, j, subText, 127, MF_BYPOSITION);
            UINT id = GetMenuItemID(sub, j);
            fwprintf(f, L"  [sub %d] id=%u \"%s\"\n", j, id, subText);
        }
    }
    fclose(f);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpMainMenu(LPVOID lpParam)
{
    (void)lpParam;
    return DumpMainMenu() ? 1 : 0;
}

// 觸發「檔案→開啟舊檔」（id=57601，DumpMainMenu 查到的真實值，非猜測）
__declspec(dllexport) bool TriggerOpenFileMenu()
{
    HWND main = FindWindowW(L"InSpecAppFrame", NULL);
    if (!main) return false;
    // WM_COMMAND 對選單命令的回傳值語意由目標決定，這裡不判斷回傳值，只送出去，
    // 由呼叫方另外偵測「開啟舊檔」視窗是否出現來確認是否成功。
    SendMessageW(main, WM_COMMAND, MAKEWPARAM(57601, 0), 0);
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_TriggerOpenFileMenu(LPVOID lpParam)
{
    (void)lpParam;
    return TriggerOpenFileMenu() ? 1 : 0;
}

// 診斷用：找到「開啟舊檔」對話框、遞迴列出其子控制項（class/id/text），
// 現代 Explorer 風格共用對話框的檔名欄位結構未知，先查證再實作正式開檔功能。
static FILE* g_openDlgDumpFile = NULL;
static int g_openDlgDumpDepth = 0;

static BOOL CALLBACK DumpOpenDialogChildProc(HWND hWnd, LPARAM lParam)
{
    wchar_t cls[64] = {0};
    wchar_t txt[512] = {0};
    GetClassNameW(hWnd, cls, 63);
    GetWindowTextW(hWnd, txt, 511);
    int id = GetDlgCtrlID(hWnd);
    if (g_openDlgDumpFile)
    {
        for (int i = 0; i < g_openDlgDumpDepth; i++) fwprintf(g_openDlgDumpFile, L"  ");
        fwprintf(g_openDlgDumpFile, L"hwnd=0x%p class=%s id=%d text=\"%s\"\n", hWnd, cls, id, txt);
    }
    g_openDlgDumpDepth++;
    EnumChildWindows(hWnd, DumpOpenDialogChildProc, 0);
    g_openDlgDumpDepth--;
    return TRUE;
}

__declspec(dllexport) bool DumpOpenDialogControls()
{
    HWND dlg = FindWindowW(NULL, L"開啟舊檔");
    if (!dlg) return false;

    _wfopen_s(&g_openDlgDumpFile, L"C:\\InSpecHookDLL_opendlg.txt", L"w, ccs=UTF-8");
    if (!g_openDlgDumpFile) return false;

    fwprintf(g_openDlgDumpFile, L"開啟舊檔對話框 hwnd=0x%p\n", dlg);
    g_openDlgDumpDepth = 1;
    EnumChildWindows(dlg, DumpOpenDialogChildProc, 0);

    fclose(g_openDlgDumpFile);
    g_openDlgDumpFile = NULL;
    return true;
}

__declspec(dllexport) DWORD WINAPI ThreadWrapper_DumpOpenDialogControls(LPVOID lpParam)
{
    (void)lpParam;
    return DumpOpenDialogControls() ? 1 : 0;
}

} // extern "C"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID reserved)
{
    (void)reserved;

    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hSelfModule = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    else if (reason == DLL_PROCESS_DETACH)
    {
        DisableSummaryPatch();
        DisableDemoButton();
        DisableLiveEditButton();
        DisableXyzEdit();
        DisableReloadButton();
        DisableApiHook();
    }
    return TRUE;
}


