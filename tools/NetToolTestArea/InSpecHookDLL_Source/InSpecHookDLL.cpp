#define UNICODE
#define _UNICODE

#include <windows.h>
#include <psapi.h>
#include <stdint.h>
#include <string>
#include <vector>
#include <intrin.h>
#include <dbghelp.h>
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
        DisableApiHook();
    }
    return TRUE;
}


