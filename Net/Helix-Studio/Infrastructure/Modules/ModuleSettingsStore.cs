using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Helix_Studio.Infrastructure.Utilities;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Infrastructure.Modules;

/// <summary>以 JSON 檔持久化單一模組之 <see cref="IModuleSettings"/> 實作。</summary>
internal sealed class ModuleSettingsStore : IModuleSettings
{
    private readonly string _filePath;
    private JsonObject _data;

    /// <summary>依模組識別碼建立儲存體並載入既有檔案。</summary>
    public ModuleSettingsStore(string moduleId)
    {
        _filePath = AppPathUtility.QueryModuleSettingsFilePath(moduleId);
        _data     = Load();
    }

    /// <inheritdoc />
    public string QueryString(string key, string fallback = "")
    {
        if (_data.TryGetPropertyValue(key, out var node) && node is JsonValue v)
            return v.GetValue<string?>() ?? fallback;
        return fallback;
    }

    /// <inheritdoc />
    public int QueryInt32(string key, int fallback = 0)
    {
        if (_data.TryGetPropertyValue(key, out var node) && node is JsonValue v)
        {
            try { return v.GetValue<int>(); } catch { }
            if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    /// <inheritdoc />
    public bool QueryBoolean(string key, bool fallback = false)
    {
        if (_data.TryGetPropertyValue(key, out var node) && node is JsonValue v)
        {
            try { return v.GetValue<bool>(); } catch { }
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    /// <inheritdoc />
    public double QueryDouble(string key, double fallback = 0.0)
    {
        if (_data.TryGetPropertyValue(key, out var node) && node is JsonValue v)
        {
            try { return v.GetValue<double>(); } catch { }
            if (double.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    /// <inheritdoc />
    public ModuleSettingSaveResult SaveString(string key, string? value)
    {
        try
        {
            _data[key] = value is null ? null : JsonValue.Create(value);
            Persist();
            return new ModuleSettingSaveResult { IsSuccess = true, SavedValue = value ?? string.Empty };
        }
        catch (Exception ex) { return ModuleSettingSaveResult.Fail(ex.Message); }
    }

    /// <inheritdoc />
    public ModuleSettingSaveResult SaveInt32(string key, int value)
    {
        try
        {
            _data[key] = JsonValue.Create(value);
            Persist();
            return new ModuleSettingSaveResult { IsSuccess = true, SavedValue = value.ToString() };
        }
        catch (Exception ex) { return ModuleSettingSaveResult.Fail(ex.Message); }
    }

    /// <inheritdoc />
    public ModuleSettingSaveResult SaveBoolean(string key, bool value)
    {
        try
        {
            _data[key] = JsonValue.Create(value);
            Persist();
            return new ModuleSettingSaveResult { IsSuccess = true, SavedValue = value.ToString() };
        }
        catch (Exception ex) { return ModuleSettingSaveResult.Fail(ex.Message); }
    }

    /// <inheritdoc />
    public ModuleSettingSaveResult SaveDouble(string key, double value)
    {
        try
        {
            _data[key] = JsonValue.Create(value);
            Persist();
            return new ModuleSettingSaveResult { IsSuccess = true, SavedValue = value.ToString() };
        }
        catch (Exception ex) { return ModuleSettingSaveResult.Fail(ex.Message); }
    }


    private JsonObject Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var text = File.ReadAllText(_filePath);
                return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
            }
        }
        catch { }
        return new JsonObject();
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var text = _data.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, text);
        }
        catch { }
    }
}
