using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    public sealed class FunctionAnalysisView : UserControl
    {
        private readonly FunctionAnalysisService functionAnalysisService;

        private Button btnStartRecording;
        private Button btnStopRecording;
        private Button btnTriggerRun;
        private DataGridView dgvFunctionList;
        private Label lblStatus;

        private bool isRecordingMode = false;
        private readonly Dictionary<string, int> rowIndexByFunctionName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Forms.Timer recordingTimer;
        private string currentRecordingActionName = string.Empty;

        private sealed class ApiCallInfo
        {
            public string ApiName { get; set; }
            public string FullContent { get; set; }
            public string CallerModule { get; set; }
            public string CallerAddress { get; set; }
            public string Time { get; set; }
        }

        private readonly List<ApiCallInfo> apiCallChain = new List<ApiCallInfo>();

        public FunctionAnalysisView(FunctionAnalysisService functionAnalysisService)
        {
            this.functionAnalysisService = functionAnalysisService;
            this.functionAnalysisService.SetLogCallback(AppendLog);

            InitializeView();
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false,
                Padding = new Padding(10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(mainLayout);

            Panel recordingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 0)
            };

            btnStartRecording = new Button
            {
                Text = "開始錄製",
                Dock = DockStyle.Left,
                Width = 120,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(34, 139, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnStartRecording.FlatAppearance.BorderSize = 0;
            btnStartRecording.Click += BtnStartRecording_Click;

            btnStopRecording = new Button
            {
                Text = "停止錄製",
                Dock = DockStyle.Left,
                Width = 120,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 20, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Margin = new Padding(10, 0, 0, 0)
            };
            btnStopRecording.FlatAppearance.BorderSize = 0;
            btnStopRecording.Click += BtnStopRecording_Click;

            recordingPanel.Controls.Add(btnStopRecording);
            recordingPanel.Controls.Add(btnStartRecording);
            mainLayout.Controls.Add(recordingPanel, 0, 0);

            btnTriggerRun = new Button
            {
                Text = "觸發 Run（使用 SendMessageW）",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 5, 0, 0)
            };
            btnTriggerRun.FlatAppearance.BorderSize = 0;
            btnTriggerRun.Click += BtnTriggerRun_Click;
            mainLayout.Controls.Add(btnTriggerRun, 0, 1);

            lblStatus = new Label
            {
                Text = "就緒",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F),
                ForeColor = Color.DarkGreen,
                Margin = new Padding(0, 5, 0, 0)
            };
            mainLayout.Controls.Add(lblStatus, 0, 2);

            Label lblFunctionList = new Label
            {
                Text = "函數列表：",
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold),
                Height = 25,
                Margin = new Padding(0, 10, 0, 5)
            };

            dgvFunctionList = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Microsoft JhengHei", 9F),
                Margin = new Padding(0, 5, 0, 0)
            };
            dgvFunctionList.Columns.Add("Function", "函式");
            dgvFunctionList.Columns.Add("FunctionName", "函式名稱");
            dgvFunctionList.Columns.Add("DllName", "DLL 名稱");
            dgvFunctionList.Columns.Add("Address", "地址");
            dgvFunctionList.Columns.Add("Time", "時間");
            dgvFunctionList.Columns["Function"].Width = 150;
            dgvFunctionList.Columns["FunctionName"].Width = 250;
            dgvFunctionList.Columns["DllName"].Width = 200;
            dgvFunctionList.Columns["Address"].Width = 150;
            dgvFunctionList.Columns["Time"].Width = 150;

            Panel functionListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };
            functionListPanel.Controls.Add(dgvFunctionList);
            functionListPanel.Controls.Add(lblFunctionList);
            mainLayout.Controls.Add(functionListPanel, 0, 3);
        }

        public void RefreshView()
        {
        }

        private void BtnTriggerRun_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "正在查找 InSpec 窗口...";
                lblStatus.ForeColor = Color.Blue;
                Application.DoEvents();

                bool success = functionAnalysisService.TriggerInSpecRun(out string errorMessage);
                if (success)
                {
                    lblStatus.Text = "[成功] 成功觸發 Run";
                    lblStatus.ForeColor = Color.DarkGreen;
                }
                else
                {
                    lblStatus.Text = "[失敗] 觸發失敗";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show($"觸發 Run 失敗：{errorMessage}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "[失敗] 發生異常";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"發生異常：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStartRecording_Click(object sender, EventArgs e)
        {
            AppendLog("========================================");
            AppendLog("開始錄製操作...");
            AppendLog("請手動操作 InSpec，所有行為將被記錄");
            AppendLog("錄製時間限制：10 秒");
            AppendLog("========================================");

            currentRecordingActionName = $"錄製_{DateTime.Now:yyyyMMdd_HHmmss}";

            btnStartRecording.Enabled = false;
            btnStopRecording.Enabled = true;

            lblStatus.Text = "錄製中...（10 秒後自動停止）";
            lblStatus.ForeColor = Color.Red;

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test", "InSpecHookDLL.dll");
            bool success = functionAnalysisService.StartRecording(true, dllPath);
            if (!success)
            {
                lblStatus.Text = "[失敗] 錄製失敗";
                lblStatus.ForeColor = Color.Red;
                btnStartRecording.Enabled = true;
                btnStopRecording.Enabled = false;
                return;
            }

            StartRecordingView();

            AppendLog("[成功] API 監控已啟用");
            AppendLog($"錄製開始時間: {DateTime.Now:HH:mm:ss}");
            AppendLog("請開始手動操作 InSpec...");

            if (recordingTimer == null)
            {
                recordingTimer = new System.Windows.Forms.Timer();
                recordingTimer.Interval = 10000;
                recordingTimer.Tick += RecordingTimer_Tick;
            }
            recordingTimer.Start();
        }

        private void RecordingTimer_Tick(object sender, EventArgs e)
        {
            recordingTimer?.Stop();
            if (functionAnalysisService.IsRecording)
            {
                BtnStopRecording_Click(sender, e);
            }
        }

        private void BtnStopRecording_Click(object sender, EventArgs e)
        {
            if (!functionAnalysisService.IsRecording)
            {
                return;
            }

            recordingTimer?.Stop();

            DateTime recordingEndTime = DateTime.Now;
            AppendLog("========================================");
            AppendLog("停止錄製操作");
            AppendLog($"錄製結束時間: {recordingEndTime:HH:mm:ss}");
            AppendLog("========================================");

            var result = functionAnalysisService.StopRecording(true);
            StopRecordingView();

            if (result != null)
            {
                AppendLog($"錄製持續時間: {result.Duration.TotalSeconds:F2} 秒");
                AppendLog("");
                AppendLog("=== 錄製摘要 ===");
                AppendLog($"錄製時間: {result.Duration.TotalSeconds:F2} 秒");
                AppendLog($"記錄的操作數: {result.RecordedActions.Count}");

                if (result.RecordedActions.Count > 0)
                {
                    AppendLog("");
                    AppendLog("記錄的操作：");
                    foreach (string action in result.RecordedActions)
                    {
                        AppendLog($"  - {action}");
                    }
                }
                else
                {
                    AppendLog("");
                    AppendLog("提示：目前只記錄基本資訊");
                    AppendLog("提示：要記錄詳細的 API 調用，請：");
                    AppendLog("  1. 確保 DLL 已注入");
                    AppendLog("  2. 勾選「啟用 API 監控」");
                    AppendLog("  3. 重新錄製操作");
                }
            }

            AppendLog("");
            AppendLog("========================================");
            AppendLog("錄製完成，所有資訊已記錄在上方日誌中");
            AppendLog("========================================");

            SaveToOutputFile();

            btnStartRecording.Enabled = true;
            btnStopRecording.Enabled = false;

            lblStatus.Text = result != null
                ? $"錄製完成（{result.Duration.TotalSeconds:F1} 秒）"
                : "錄製完成";
            lblStatus.ForeColor = Color.DarkGreen;
        }

        private void SaveToOutputFile()
        {
            if (apiCallChain.Count == 0)
            {
                return;
            }

            try
            {
                string currentDir = Directory.GetCurrentDirectory();
                string outputPath = Path.Combine(currentDir, "output.txt");

                using (StreamWriter writer = new StreamWriter(outputPath, true, Encoding.UTF8))
                {
                    writer.WriteLine($"動作 : {currentRecordingActionName}");
                    writer.WriteLine();

                    var sortedCalls = apiCallChain.OrderBy(c => c.Time).ToList();
                    var uniqueCalls = new Dictionary<string, List<ApiCallInfo>>();

                    foreach (var call in sortedCalls)
                    {
                        string key = GetApiDisplayInfo(call.FullContent);
                        if (!uniqueCalls.ContainsKey(key))
                        {
                            uniqueCalls[key] = new List<ApiCallInfo>();
                        }
                        uniqueCalls[key].Add(call);
                    }

                    var processed = new HashSet<ApiCallInfo>();
                    foreach (var call in sortedCalls)
                    {
                        if (processed.Contains(call)) continue;

                        string key = GetApiDisplayInfo(call.FullContent);
                        var similarCalls = uniqueCalls[key];

                        if (similarCalls.Count > 1 && similarCalls.Count <= 10)
                        {
                            writer.WriteLine($"[API] {key}");
                            foreach (var similar in similarCalls.Skip(1))
                            {
                                processed.Add(similar);
                            }
                        }
                        else if (similarCalls.Count > 10)
                        {
                            writer.WriteLine($"[API] {key} (出現 {similarCalls.Count} 次，已省略重複)");
                            foreach (var similar in similarCalls.Skip(1))
                            {
                                processed.Add(similar);
                            }
                        }
                        else
                        {
                            writer.WriteLine($"[API] {key}");
                        }

                        processed.Add(call);
                    }

                    writer.WriteLine();
                }

                AppendLog($"[成功] 已將錄製結果寫入: {Path.Combine(Directory.GetCurrentDirectory(), "output.txt")}");
            }
            catch (Exception ex)
            {
                AppendLog($"[失敗] 寫入 output.txt 時發生錯誤: {ex.Message}");
            }
        }

        private static string GetApiDisplayInfo(string fullContent)
        {
            int apiStart = fullContent.IndexOf("[API]");
            string apiInfo = apiStart < 0 ? fullContent : fullContent.Substring(apiStart + 5).Trim();
            int callerIndex = apiInfo.IndexOf(" <- ");
            if (callerIndex >= 0)
            {
                apiInfo = apiInfo.Substring(0, callerIndex).Trim();
            }
            return apiInfo;
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            AppendLogOnUiThread(message);
        }

        private void AppendLogOnUiThread(string message)
        {
            if (isRecordingMode)
            {
                TryUpdateFunctionListFromLog(message);
            }
        }

        private void StartRecordingView()
        {
            isRecordingMode = true;
            rowIndexByFunctionName.Clear();
            apiCallChain.Clear();
            dgvFunctionList?.Rows.Clear();
        }

        private void StopRecordingView()
        {
            isRecordingMode = false;
        }

        private void TryUpdateFunctionListFromLog(string message)
        {
            if (dgvFunctionList == null) return;
            if (string.IsNullOrWhiteSpace(message)) return;
            if (message.StartsWith("[DLL] [UPDATE_TIME]")) return;

            int apiStart = message.IndexOf("[API]");
            int hookStart = message.IndexOf("[HOOK]");
            int tagStart = apiStart >= 0 ? apiStart : hookStart;
            if (tagStart < 0) return;

            string content = message.Substring(tagStart).Trim();
            string functionName = ExtractFunctionName(content);
            if (string.IsNullOrWhiteSpace(functionName)) return;

            string time = ExtractDllTime(message) ?? DateTime.Now.ToString("HH:mm:ss.fff");

            string functionCell = BuildCompactFunctionCell(content);
            string addressCell = BuildCompactAddressCell(content);

            string callerModule = null;
            string callerAddress = null;
            int callerStart = content.IndexOf(" <- ");
            if (callerStart >= 0)
            {
                string callerPart = content.Substring(callerStart + 4).Trim();
                int plusIndex = callerPart.IndexOf('+');
                if (plusIndex > 0)
                {
                    callerModule = callerPart.Substring(0, plusIndex).Trim();
                    if (plusIndex + 1 < callerPart.Length)
                    {
                        string addressPart = callerPart.Substring(plusIndex + 1).Trim();
                        int spaceIndex = addressPart.IndexOf(' ');
                        callerAddress = spaceIndex > 0 ? addressPart.Substring(0, spaceIndex) : addressPart;
                    }
                }
                else
                {
                    callerModule = callerPart;
                }
            }

            apiCallChain.Add(new ApiCallInfo
            {
                ApiName = functionName,
                FullContent = content,
                CallerModule = callerModule,
                CallerAddress = callerAddress,
                Time = time
            });

            if (!rowIndexByFunctionName.TryGetValue(functionName, out int rowIndex) || rowIndex < 0 || rowIndex >= dgvFunctionList.Rows.Count)
            {
                int newRowIndex = dgvFunctionList.Rows.Add(functionCell, functionName, string.Empty, addressCell, time);
                rowIndexByFunctionName[functionName] = newRowIndex;
                return;
            }

            DataGridViewRow row = dgvFunctionList.Rows[rowIndex];
            row.Cells["Function"].Value = functionCell;
            row.Cells["Address"].Value = addressCell;
            row.Cells["Time"].Value = time;
        }

        private static string BuildCompactFunctionCell(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            int classIdx = content.IndexOf(" class=\"", StringComparison.OrdinalIgnoreCase);
            int titleIdx = content.IndexOf(" title=\"", StringComparison.OrdinalIgnoreCase);
            int hwndIdx = content.IndexOf(" hWnd=", StringComparison.OrdinalIgnoreCase);

            string hWndPart = hwndIdx >= 0 ? ExtractToken(content, hwndIdx + 1) : null;
            string classPart = classIdx >= 0 ? ExtractQuotedValue(content, classIdx + " class=\"".Length - 1) : null;
            string titlePart = titleIdx >= 0 ? ExtractQuotedValue(content, titleIdx + " title=\"".Length - 1) : null;

            if (string.IsNullOrWhiteSpace(hWndPart) && string.IsNullOrWhiteSpace(classPart) && string.IsNullOrWhiteSpace(titlePart))
            {
                return content;
            }

            string titleTrimmed = string.IsNullOrWhiteSpace(titlePart) ? string.Empty : TrimToMaxLength(titlePart, 80);
            string classTrimmed = string.IsNullOrWhiteSpace(classPart) ? string.Empty : TrimToMaxLength(classPart, 60);

            string windowInfo = $"{(string.IsNullOrWhiteSpace(hWndPart) ? string.Empty : hWndPart)}";
            if (!string.IsNullOrWhiteSpace(classTrimmed))
            {
                windowInfo += (windowInfo.Length > 0 ? " " : string.Empty) + $"class=\"{classTrimmed}\"";
            }
            if (!string.IsNullOrWhiteSpace(titleTrimmed))
            {
                windowInfo += (windowInfo.Length > 0 ? " " : string.Empty) + $"title=\"{titleTrimmed}\"";
            }

            return windowInfo;
        }

        private static string BuildCompactAddressCell(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            int msgIdx = content.IndexOf(" Msg=", StringComparison.OrdinalIgnoreCase);
            int wParamIdx = content.IndexOf(" wParam=", StringComparison.OrdinalIgnoreCase);
            int lParamIdx = content.IndexOf(" lParam=", StringComparison.OrdinalIgnoreCase);

            string msgPart = msgIdx >= 0 ? ExtractToken(content, msgIdx + 1) : null;
            string wParamPart = wParamIdx >= 0 ? ExtractToken(content, wParamIdx + 1) : null;
            string lParamPart = lParamIdx >= 0 ? ExtractToken(content, lParamIdx + 1) : null;

            string compact = string.Empty;
            if (!string.IsNullOrWhiteSpace(msgPart))
            {
                compact = msgPart;
            }
            if (!string.IsNullOrWhiteSpace(wParamPart))
            {
                compact += (compact.Length > 0 ? " " : string.Empty) + wParamPart;
            }
            if (!string.IsNullOrWhiteSpace(lParamPart))
            {
                compact += (compact.Length > 0 ? " " : string.Empty) + lParamPart;
            }

            return string.IsNullOrWhiteSpace(compact) ? string.Empty : compact;
        }

        private static string ExtractQuotedValue(string content, int quoteStartIndex)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            if (quoteStartIndex < 0 || quoteStartIndex >= content.Length) return null;

            int firstQuote = content.IndexOf('"', quoteStartIndex);
            if (firstQuote < 0 || firstQuote + 1 >= content.Length) return null;
            int secondQuote = content.IndexOf('"', firstQuote + 1);
            if (secondQuote <= firstQuote) return null;
            return content.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static string ExtractToken(string content, int startIndex)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            if (startIndex < 0 || startIndex >= content.Length) return null;

            int end = content.IndexOf(' ', startIndex);
            return end > startIndex ? content.Substring(startIndex, end - startIndex).Trim() : content.Substring(startIndex).Trim();
        }

        private static string TrimToMaxLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private static string ExtractDllTime(string message)
        {
            const string prefix = "[DLL] [";
            if (string.IsNullOrEmpty(message) || !message.StartsWith(prefix))
            {
                return null;
            }

            int end = message.IndexOf(']', prefix.Length);
            return end > prefix.Length ? message.Substring(prefix.Length, end - prefix.Length).Trim() : null;
        }

        private static string ExtractFunctionName(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            int tagEnd = content.IndexOf(']');
            string afterTag = tagEnd >= 0 && tagEnd + 1 < content.Length ? content.Substring(tagEnd + 1).Trim() : content.Trim();
            if (string.IsNullOrWhiteSpace(afterTag)) return null;

            int paren = afterTag.IndexOf('(');
            if (paren > 0)
            {
                return afterTag.Substring(0, paren).Trim();
            }

            int space = afterTag.IndexOf(' ');
            return space > 0 ? afterTag.Substring(0, space).Trim() : afterTag.Trim();
        }
    }
}


