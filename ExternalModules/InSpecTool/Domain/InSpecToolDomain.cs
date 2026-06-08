using System.IO;
using System.Text.Json;
using ExternalModules.InSpecTool.Model;

namespace ExternalModules.InSpecTool.Domain;

/// <summary>
/// 提供 InSpecTool 業務規則、字串判斷與資料轉換的統一入口。
/// </summary>
internal sealed class InSpecToolDomain
{
    private static readonly FixedFieldTemplate[] FixedFieldTemplates =
    [
        new("Part Number", "part number", true, InSpecFieldDefinitionModel.DirectInputTargetMode),
        new("Lot Number", "lot number", true, InSpecFieldDefinitionModel.DirectInputTargetMode),
        new("Press", "press", true, InSpecFieldDefinitionModel.PromptConfirmationTargetMode),
        new("Machine", "machine", true, InSpecFieldDefinitionModel.DirectInputTargetMode),
        new("Manufacture Date", "manufacture date", true, InSpecFieldDefinitionModel.DirectInputTargetMode),
        new("Expiration Date", "expiration date", true, InSpecFieldDefinitionModel.DirectInputTargetMode)
    ];

    private static readonly string[][] PartNumberKeywordSets =
    [
        ["part", "number"],
        ["料號"]
    ];

    private static readonly string[][] LotKeywordSets =
    [
        ["lot"],
        ["批號"]
    ];

    private static readonly string[][] PressKeywordSets =
    [
        ["press"],
        ["壓合"]
    ];

    /// <summary>
    /// 解析條碼內容為欄位值快照。
    /// </summary>
    /// <param name="barcode">原始條碼內容。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>條碼解析結果模型。</returns>
    public InSpecBarcodeParseResultModel QueryBarcodeParseResult(string barcode, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
    {
        var fieldValues = Enumerable.Repeat(string.Empty, fieldDefinitions.Count).ToArray();
        var rawBarcode = barcode ?? string.Empty;
        if (fieldDefinitions.Count == 0)
        {
            return new InSpecBarcodeParseResultModel
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = string.Empty,
                FieldValues = fieldValues,
                Parts = []
            };
        }

        var cleanedBarcode = rawBarcode.Trim();
        if (string.IsNullOrWhiteSpace(cleanedBarcode))
        {
            return new InSpecBarcodeParseResultModel
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = string.Empty,
                FieldValues = fieldValues,
                Parts = []
            };
        }

        var hasLeadingSeparator = cleanedBarcode.StartsWith('|');
        if (hasLeadingSeparator)
        {
            cleanedBarcode = cleanedBarcode.TrimStart('|');
        }

        if (cleanedBarcode.Contains('_'))
        {
            return new InSpecBarcodeParseResultModel
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = cleanedBarcode,
                FieldValues = fieldValues,
                Parts = [],
                HasBlockedCharacter = true,
                HasLeadingSeparator = hasLeadingSeparator
            };
        }

        var parts = cleanedBarcode.Split('|', StringSplitOptions.TrimEntries);
        for (var index = 0; index < Math.Min(parts.Length, fieldDefinitions.Count); index++)
        {
            fieldValues[index] = QueryIsPartNumberField(fieldDefinitions[index].Name, fieldDefinitions[index].Keyword)
                ? ResolveNormalizedPartNumberForQuery(parts[index])
                : parts[index];
        }

        return new InSpecBarcodeParseResultModel
        {
            RawBarcode = rawBarcode,
            CleanedBarcode = cleanedBarcode,
            Parts = parts,
            FieldValues = fieldValues,
            HasLeadingSeparator = hasLeadingSeparator
        };
    }

    /// <summary>
    /// 驗證條碼解析結果是否可用，並回傳錯誤訊息。
    /// </summary>
    /// <param name="parseResult">條碼解析結果。</param>
    /// <returns>空字串表示可用，否則為錯誤訊息。</returns>
    public string ValidateBarcodeParseResult(InSpecBarcodeParseResultModel parseResult)
    {
        if (!parseResult.HasInput)
        {
            return string.Empty;
        }

        if (parseResult.HasBlockedCharacter)
        {
            return "條碼格式不正確，包含不允許的字元。";
        }

        return string.Empty;
    }

    /// <summary>
    /// 依條碼解析結果與欄位定義建立顯示欄位集合。
    /// </summary>
    /// <param name="parseResult">條碼解析結果。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>欄位顯示模型集合。</returns>
    public IReadOnlyList<InSpecPreviewFieldModel> QueryPreviewFieldModels(
        InSpecBarcodeParseResultModel parseResult,
        IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
    {
        return fieldDefinitions
            .Select((fieldDefinition, index) => new InSpecPreviewFieldModel
            {
                Label = fieldDefinition.Name,
                Value = index < parseResult.FieldValues.Count ? parseResult.FieldValues[index] : string.Empty,
                IsReadOnly = !fieldDefinition.IsEditable,
                Keyword = fieldDefinition.Keyword,
                TargetMode = fieldDefinition.TargetMode
            })
            .ToArray();
    }

    /// <summary>
    /// 將欄位值集合組合回條碼字串。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>組合後的條碼字串。</returns>
    public string ResolveBarcodeText(IEnumerable<InSpecPreviewFieldModel> fieldModels, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
    {
        var orderedFieldDefinitions = fieldDefinitions.ToArray();
        if (orderedFieldDefinitions.Length == 0)
        {
            return string.Empty;
        }

        var fieldValueMap = fieldModels.ToDictionary(fieldModel => fieldModel.Label, fieldModel => fieldModel.Value, StringComparer.OrdinalIgnoreCase);
        var parts = orderedFieldDefinitions
            .Select(fieldDefinition =>
            {
                if (!fieldValueMap.TryGetValue(fieldDefinition.Name, out var value))
                {
                    return string.Empty;
                }

                return value?.Trim() ?? string.Empty;
            })
            .ToArray();

        return $"|{string.Join("|", parts)}|";
    }

    /// <summary>
    /// 解析欄位定義設定字串。
    /// </summary>
    /// <param name="rawSchema">原始欄位設定字串。</param>
    /// <returns>欄位定義集合。</returns>
    public IReadOnlyList<InSpecFieldDefinitionModel> QueryFieldDefinitions(string rawSchema)
    {
        var source = string.IsNullOrWhiteSpace(rawSchema) ? string.Empty : rawSchema.Trim();
        var fieldDefinitions = source.StartsWith("[", StringComparison.Ordinal)
            ? QueryJsonFieldDefinitions(source)
            : QueryLegacyFieldDefinitions(source);

        return NormalizeFixedFieldDefinitions(fieldDefinitions);
    }

    /// <summary>
    /// 正規化欄位目標模式字串。
    /// </summary>
    /// <param name="rawTargetMode">原始模式字串。</param>
    /// <returns>正規化後的模式字串。</returns>
    public string ResolveTargetMode(string? rawTargetMode)
    {
        if (string.IsNullOrWhiteSpace(rawTargetMode))
        {
            return InSpecFieldDefinitionModel.DirectInputTargetMode;
        }

        var trimmedText = rawTargetMode.Trim();
        if (string.Equals(trimmedText, "Direct Input", StringComparison.OrdinalIgnoreCase))
        {
            return InSpecFieldDefinitionModel.DirectInputTargetMode;
        }

        if (string.Equals(trimmedText, "Prompt Confirmation", StringComparison.OrdinalIgnoreCase))
        {
            return InSpecFieldDefinitionModel.PromptConfirmationTargetMode;
        }

        return trimmedText;
    }

    /// <summary>
    /// 判斷欄位定義是否需要提示確認。
    /// </summary>
    /// <param name="fieldDefinition">欄位定義模型。</param>
    /// <returns>若需要提示確認則回傳 <c>true</c>。</returns>
    public bool ValidateRequiresConfirmation(InSpecFieldDefinitionModel fieldDefinition)
        => string.Equals(ResolveTargetMode(fieldDefinition.TargetMode), InSpecFieldDefinitionModel.PromptConfirmationTargetMode, StringComparison.Ordinal);

    /// <summary>
    /// 判斷欄位顯示模型是否需要提示確認。
    /// </summary>
    /// <param name="fieldModel">欄位顯示模型。</param>
    /// <returns>若需要提示確認則回傳 <c>true</c>。</returns>
    public bool ValidateRequiresConfirmation(InSpecPreviewFieldModel fieldModel)
        => string.Equals(ResolveTargetMode(fieldModel.TargetMode), InSpecFieldDefinitionModel.PromptConfirmationTargetMode, StringComparison.Ordinal);

    /// <summary>
    /// 解析主頁顯示的自動監控狀態文字。
    /// </summary>
    /// <param name="configuration">目前組態。</param>
    /// <param name="hasShellContext">是否已啟用模組上下文。</param>
    /// <returns>顯示用狀態文字。</returns>
    public string ResolveAutomationStatusText(InSpecToolConfigurationModel configuration, bool hasShellContext)
    {
        if (!hasShellContext)
        {
            return "自動監控：待模組啟用";
        }

        return configuration.EnableAutoMonitoring
            ? $"自動監控：開啟（{Math.Clamp(configuration.MonitorIntervalMs, 100, 10000)} ms）"
            : "自動監控：關閉";
    }

    /// <summary>
    /// 解析檔案列表標題文字。
    /// </summary>
    /// <param name="isSearching">是否正在搜尋。</param>
    /// <returns>顯示用標題文字。</returns>
    public string ResolveFileListTitleText(bool isSearching) => isSearching ? "檔案結果（搜尋中...）" : "檔案結果";

    /// <summary>
    /// 判斷是否應顯示檔案列表提示訊息。
    /// </summary>
    /// <param name="placeholderText">提示文字內容。</param>
    /// <param name="resultCount">搜尋結果數量。</param>
    /// <returns>若應顯示提示訊息則回傳 <c>true</c>。</returns>
    public bool ValidatePlaceholderVisible(string placeholderText, int resultCount)
        => !string.IsNullOrWhiteSpace(placeholderText) && resultCount == 0;

    /// <summary>
    /// 解析檔案查詢所需的料號關鍵字。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <returns>料號關鍵字。</returns>
    public string ResolveSearchPartNumber(IReadOnlyList<InSpecPreviewFieldModel> fieldModels)
    {
        var partNumberField = fieldModels.FirstOrDefault(fieldModel =>
            QueryIsPartNumberField(fieldModel.Label, fieldModel.Keyword));
        if (partNumberField is not null)
        {
            return partNumberField.Value.Trim();
        }

        return fieldModels.FirstOrDefault()?.Value.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 正規化料號內容以供檔案查詢。
    /// </summary>
    /// <param name="partNumber">原始料號。</param>
    /// <returns>正規化後的料號。</returns>
    public string ResolveNormalizedPartNumberForQuery(string partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return string.Empty;
        }

        var normalizedPartNumber = partNumber.Trim().ToUpperInvariant();
        var lastDashIndex = normalizedPartNumber.LastIndexOf('-');
        if (lastDashIndex < 0 || lastDashIndex >= normalizedPartNumber.Length - 1)
        {
            return normalizedPartNumber;
        }

        var trailingText = normalizedPartNumber[(lastDashIndex + 1)..];
        var letters = new List<char>();
        foreach (var character in trailingText)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            letters.Add(character);
        }

        return letters.Count == 0
            ? normalizedPartNumber
            : normalizedPartNumber[..(lastDashIndex + 1)] + new string([.. letters]);
    }

    /// <summary>
    /// 判斷目前欄位是否允許開啟檔案。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="message">驗證失敗訊息。</param>
    /// <returns>若允許開啟檔案則回傳 <c>true</c>。</returns>
    public bool ValidateCanOpenFile(IEnumerable<InSpecPreviewFieldModel> fieldModels, out string message)
    {
        var missingFieldNames = fieldModels
            .Where(fieldModel => !ValidateRequiresConfirmation(fieldModel) && string.IsNullOrWhiteSpace(fieldModel.Value))
            .Select(fieldModel => fieldModel.Label)
            .ToArray();

        if (missingFieldNames.Length == 0)
        {
            message = string.Empty;
            return true;
        }

        message = $"請先完成以下欄位後再開啟檔案：{string.Join("、", missingFieldNames)}";
        return false;
    }

    /// <summary>
    /// 解析橋接輸出目標路徑。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="configuration">目前組態。</param>
    /// <returns>完整目標路徑，若資料不足則回傳 <c>null</c>。</returns>
    public string? ResolveCurrentTargetPath(IEnumerable<InSpecPreviewFieldModel> fieldModels, InSpecToolConfigurationModel configuration)
    {
        var orderedFieldModels = fieldModels.ToArray();
        var partNumber = ResolveSpecialFieldValue(orderedFieldModels, fieldModel => QueryIsPartNumberField(fieldModel.Label, fieldModel.Keyword));
        var lotNumber = ResolveSpecialFieldValue(orderedFieldModels, fieldModel => QueryIsLotField(fieldModel.Label, fieldModel.Keyword));
        var pressName = ResolveSpecialFieldValue(orderedFieldModels, fieldModel => QueryIsPressField(fieldModel.Label, fieldModel.Keyword));

        if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(lotNumber) || string.IsNullOrWhiteSpace(pressName))
        {
            return null;
        }

        var safePartNumber = ResolveSafeFileNamePart(partNumber);
        var safeLotNumber = ResolveSafeFileNamePart(lotNumber);
        if (string.IsNullOrWhiteSpace(safePartNumber) || string.IsNullOrWhiteSpace(safeLotNumber))
        {
            return null;
        }

        var trimmedPressName = pressName.Trim();
        var dashIndex = trimmedPressName.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            trimmedPressName = trimmedPressName[..dashIndex];
        }

        var safePressDirectory = ResolveSafeFileNamePart(trimmedPressName);
        if (string.IsNullOrWhiteSpace(safePressDirectory))
        {
            return null;
        }

        var partGroupFolder = ResolveSafeFileNamePart(ResolvePartGroupFolder(safePartNumber));
        if (string.IsNullOrWhiteSpace(partGroupFolder))
        {
            return null;
        }

        var rootDirectory = !string.IsNullOrWhiteSpace(configuration.SystemLogDirectory)
            ? configuration.SystemLogDirectory.Trim()
            : configuration.BackupSavePath.Trim();
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        var fileName = $"{safePartNumber}_{safeLotNumber}.txt";
        var partPrefix = safePartNumber[..1];
        return Path.GetFullPath(Path.Combine(rootDirectory, safePressDirectory, partPrefix, partGroupFolder, fileName));
    }

    /// <summary>
    /// 建立設定頁顯示所需的固定選項集合。
    /// </summary>
    /// <returns>設定頁選項模型。</returns>
    public InSpecSettingsOptionModel QuerySettingsOptionModel()
    {
        return new InSpecSettingsOptionModel
        {
            ScanIntervalOptions = ["80", "120", "160", "200", "300", "500"],
            MonitorIntervalOptions = ["200", "300", "500", "800", "1000"],
            TargetModeOptions =
            [
                InSpecFieldDefinitionModel.DirectInputTargetMode,
                InSpecFieldDefinitionModel.PromptConfirmationTargetMode
            ]
        };
    }

    /// <summary>
    /// 將欄位結構列序列化為設定字串。
    /// </summary>
    /// <param name="fieldRows">欄位結構列集合。</param>
    /// <returns>可儲存的 JSON 字串。</returns>
    public string ResolveFieldSchemaText(IEnumerable<InSpecSettingsFieldRowModel> fieldRows)
    {
        var payload = fieldRows
            .Where(fieldRow => !string.IsNullOrWhiteSpace(fieldRow.Name))
            .Select(fieldRow => new
            {
                name = fieldRow.Name.Trim(),
                keyword = string.IsNullOrWhiteSpace(fieldRow.Keyword) ? fieldRow.Name.Trim() : fieldRow.Keyword.Trim(),
                targetMode = ResolveTargetMode(fieldRow.TargetMode),
                isEditable = fieldRow.IsEditable,
                isLocked = fieldRow.IsLocked
            })
            .ToArray();

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// 將欄位定義集合轉為設定頁欄位結構列集合。
    /// </summary>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>設定頁欄位結構列集合。</returns>
    public IReadOnlyList<InSpecSettingsFieldRowModel> QuerySettingsFieldRows(IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => fieldDefinitions.Select(fieldDefinition => new InSpecSettingsFieldRowModel
            {
                Name = fieldDefinition.Name,
                Keyword = fieldDefinition.Keyword,
                TargetMode = ResolveTargetMode(fieldDefinition.TargetMode),
                IsEditable = fieldDefinition.IsEditable,
                IsLocked = fieldDefinition.IsLocked
            }).ToArray();

    /// <summary>
    /// 建立新的設定頁欄位結構列。
    /// </summary>
    /// <returns>新的欄位結構列模型。</returns>
    public InSpecSettingsFieldRowModel CreateSettingsFieldRow()
    {
        return new InSpecSettingsFieldRowModel
        {
            TargetMode = InSpecFieldDefinitionModel.DirectInputTargetMode,
            IsEditable = true,
            IsLocked = false
        };
    }

    /// <summary>
    /// 由欄位名稱與關鍵字判斷是否為料號欄位。
    /// </summary>
    /// <param name="candidates">候選字串集合。</param>
    /// <returns>若符合料號欄位規則則回傳 <c>true</c>。</returns>
    public bool QueryIsPartNumberField(params string?[] candidates) => QueryMatchesKeywordSet(candidates, PartNumberKeywordSets);

    /// <summary>
    /// 由欄位名稱與關鍵字判斷是否為批號欄位。
    /// </summary>
    /// <param name="candidates">候選字串集合。</param>
    /// <returns>若符合批號欄位規則則回傳 <c>true</c>。</returns>
    public bool QueryIsLotField(params string?[] candidates) => QueryMatchesKeywordSet(candidates, LotKeywordSets);

    /// <summary>
    /// 由欄位名稱與關鍵字判斷是否為壓合欄位。
    /// </summary>
    /// <param name="candidates">候選字串集合。</param>
    /// <returns>若符合壓合欄位規則則回傳 <c>true</c>。</returns>
    public bool QueryIsPressField(params string?[] candidates) => QueryMatchesKeywordSet(candidates, PressKeywordSets);

    private IReadOnlyList<InSpecFieldDefinitionModel> QueryJsonFieldDefinitions(string rawSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(rawSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var fieldDefinitions = new List<InSpecFieldDefinitionModel>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = QueryJsonString(element, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                fieldDefinitions.Add(new InSpecFieldDefinitionModel
                {
                    Name = name,
                    Keyword = QueryJsonString(element, "keyword", name),
                    TargetMode = ResolveTargetMode(QueryJsonString(element, "targetMode", InSpecFieldDefinitionModel.DirectInputTargetMode)),
                    IsEditable = QueryJsonBoolean(element, "isEditable", true),
                    IsLocked = QueryJsonBoolean(element, "isLocked", false)
                });
            }

            return fieldDefinitions;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private IReadOnlyList<InSpecFieldDefinitionModel> QueryLegacyFieldDefinitions(string rawSchema)
    {
        if (string.IsNullOrWhiteSpace(rawSchema))
        {
            return [];
        }

        var fieldDefinitions = new List<InSpecFieldDefinitionModel>();
        foreach (var segment in rawSchema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(',', 4, StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var isEditable = true;
            if (parts.Length > 1 && bool.TryParse(parts[1], out var parsedEditable))
            {
                isEditable = parsedEditable;
            }

            var targetMode = ResolveTargetMode(parts.Length > 2 ? parts[2] : InSpecFieldDefinitionModel.DirectInputTargetMode);
            var isLocked = false;
            if (parts.Length > 3 && bool.TryParse(parts[3], out var parsedLocked))
            {
                isLocked = parsedLocked;
            }
            fieldDefinitions.Add(new InSpecFieldDefinitionModel
            {
                Name = parts[0],
                Keyword = parts[0],
                IsEditable = isEditable,
                TargetMode = targetMode,
                IsLocked = isLocked
            });
        }

        return fieldDefinitions;
    }

    private IReadOnlyList<InSpecFieldDefinitionModel> NormalizeFixedFieldDefinitions(IReadOnlyList<InSpecFieldDefinitionModel> sourceDefinitions)
    {
        var normalizedDefinitions = new List<InSpecFieldDefinitionModel>(FixedFieldTemplates.Length);
        for (var index = 0; index < FixedFieldTemplates.Length; index++)
        {
            var template = FixedFieldTemplates[index];
            var sourceDefinition = QueryMatchingFixedField(sourceDefinitions, template, index);
            normalizedDefinitions.Add(new InSpecFieldDefinitionModel
            {
                Name = template.Name,
                Keyword = template.Keyword,
                IsEditable = sourceDefinition?.IsEditable ?? template.IsEditable,
                TargetMode = ResolveTargetMode(sourceDefinition?.TargetMode ?? template.TargetMode),
                IsLocked = true
            });
        }

        return normalizedDefinitions;
    }

    private InSpecFieldDefinitionModel? QueryMatchingFixedField(
        IReadOnlyList<InSpecFieldDefinitionModel> sourceDefinitions,
        FixedFieldTemplate template,
        int index)
    {
        if (index < sourceDefinitions.Count)
        {
            return sourceDefinitions[index];
        }

        return sourceDefinitions.FirstOrDefault(sourceDefinition =>
            string.Equals(sourceDefinition.Name, template.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceDefinition.Keyword, template.Keyword, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<InSpecFieldDefinitionModel> QueryDefaultFieldDefinitions()
    {
        return
        [
            new InSpecFieldDefinitionModel
            {
                Name = "料號",
                Keyword = "part number",
                IsEditable = true,
                TargetMode = InSpecFieldDefinitionModel.DirectInputTargetMode
            },
            new InSpecFieldDefinitionModel
            {
                Name = "批號",
                Keyword = "lot number",
                IsEditable = true,
                TargetMode = InSpecFieldDefinitionModel.DirectInputTargetMode
            },
            new InSpecFieldDefinitionModel
            {
                Name = "壓合",
                Keyword = "press",
                IsEditable = false,
                TargetMode = InSpecFieldDefinitionModel.PromptConfirmationTargetMode
            },
            new InSpecFieldDefinitionModel
            {
                Name = "機台",
                Keyword = "machine",
                IsEditable = true,
                TargetMode = InSpecFieldDefinitionModel.DirectInputTargetMode
            },
            new InSpecFieldDefinitionModel
            {
                Name = "操作員",
                Keyword = "operator",
                IsEditable = true,
                TargetMode = InSpecFieldDefinitionModel.DirectInputTargetMode
            }
        ];
    }

    private string ResolveSpecialFieldValue(IEnumerable<InSpecPreviewFieldModel> fieldModels, Func<InSpecPreviewFieldModel, bool> validateField)
    {
        foreach (var fieldModel in fieldModels)
        {
            if (validateField(fieldModel))
            {
                return fieldModel.Value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private string ResolvePartGroupFolder(string partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return string.Empty;
        }

        var dashIndex = partNumber.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= partNumber.Length - 1)
        {
            return partNumber.Trim();
        }

        var prefix = partNumber[..dashIndex].Trim();
        var suffix = partNumber[(dashIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(suffix))
        {
            return partNumber.Trim();
        }

        return $"{prefix}-{suffix[0]}";
    }

    private string ResolveSafeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitizedText = value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            sanitizedText = sanitizedText.Replace(invalidCharacter, '_');
        }

        return sanitizedText.TrimEnd('.', ' ').Trim();
    }

    private string QueryJsonString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return defaultValue;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private bool QueryJsonBoolean(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsedValue) => parsedValue,
            _ => defaultValue
        };
    }

    private bool QueryMatchesKeywordSet(IEnumerable<string?> candidates, IEnumerable<string[]> keywordSets)
    {
        var source = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate!.Trim())
            .ToArray();
        if (source.Length == 0)
        {
            return false;
        }

        foreach (var keywordSet in keywordSets)
        {
            foreach (var candidate in source)
            {
                if (keywordSet.All(keyword => candidate.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record FixedFieldTemplate(
        string Name,
        string Keyword,
        bool IsEditable,
        string TargetMode);
}
