using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Text;

namespace NetToolTestArea.Services
{
    public sealed class IsolatedStorageService
    {
        private const string CurrentTargetDirectoryName = "InSpecTool";
        private const string CurrentTargetFileName = "current-target.txt";

        private static string QueryRelativeCurrentTargetPath()
        {
            return Path.Combine(CurrentTargetDirectoryName, CurrentTargetFileName);
        }

        public void SaveCurrentTargetPath(string targetPath)
        {
            string normalizedTargetPath = (targetPath ?? string.Empty).Trim();
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                EnsureDirectoryExists(store);
                using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(
                    QueryRelativeCurrentTargetPath(),
                    FileMode.Create,
                    FileAccess.Write,
                    store))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(normalizedTargetPath);
                }
            }
        }

        public string LoadCurrentTargetPath()
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                if (!store.FileExists(QueryRelativeCurrentTargetPath()))
                {
                    return string.Empty;
                }

                using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(
                    QueryRelativeCurrentTargetPath(),
                    FileMode.Open,
                    FileAccess.Read,
                    store))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return (reader.ReadToEnd() ?? string.Empty).Trim();
                }
            }
        }

        public bool QueryHasCurrentTargetPath()
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                return store.FileExists(QueryRelativeCurrentTargetPath());
            }
        }

        public void DeleteCurrentTargetPath()
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                if (store.FileExists(QueryRelativeCurrentTargetPath()))
                {
                    store.DeleteFile(QueryRelativeCurrentTargetPath());
                }
            }
        }

        public string AppendInputToCurrentTarget(string inputContent)
        {
            string targetPath = LoadCurrentTargetPath();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new InvalidOperationException("目前沒有可用的 CurrentTargetPath。");
            }

            string fullTargetPath = Path.GetFullPath(targetPath);
            string parentDirectory = Path.GetDirectoryName(fullTargetPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            using (FileStream stream = new FileStream(fullTargetPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(inputContent ?? string.Empty);
            }

            return fullTargetPath;
        }

        public string QueryDiagnosticText()
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                string storedPath = LoadCurrentTargetPath();
                StringBuilder result = new StringBuilder();
                result.AppendLine("IsolatedStorage 狀態");
                result.AppendLine($"Scope: User + Assembly");
                result.AppendLine($"Directory: {CurrentTargetDirectoryName}");
                result.AppendLine($"File: {CurrentTargetFileName}");
                result.AppendLine($"Exists: {store.FileExists(QueryRelativeCurrentTargetPath())}");
                result.AppendLine($"StoredTarget: {(string.IsNullOrWhiteSpace(storedPath) ? "(空白)" : storedPath)}");
                return result.ToString();
            }
        }

        private static void EnsureDirectoryExists(IsolatedStorageFile store)
        {
            if (!store.DirectoryExists(CurrentTargetDirectoryName))
            {
                store.CreateDirectory(CurrentTargetDirectoryName);
            }
        }
    }
}
