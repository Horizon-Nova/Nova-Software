using System;
using System.Drawing;
using System.Windows.Forms;
using NetToolTestArea.Model;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    public sealed class KeyToolView : UserControl
    {
        private readonly KeyToolService keyToolService;

        private SplitContainer keyToolSplit;
        private TextBox txtModuleId;
        private TextBox txtGuidKey;
        private TextBox txtCpuProcessorId;
        private TextBox txtSmbiosUuid;
        private TextBox txtBiosSerial;
        private TextBox txtBaseBoardSerial;
        private TextBox txtVolumeSerial;
        private TextBox txtPhysicalNicMac;

        private Button btnDetectLocal;
        private Button btnGenerate;
        private Button btnVerify;
        private Button btnCopy;
        private TextBox txtVerifyKey;
        private TextBox txtKeyResult;
        private Label lblStatus;
        private Label lblScoreSummary;

        private string currentKey = string.Empty;

        public KeyToolView(KeyToolService keyToolService)
        {
            this.keyToolService = keyToolService;
            InitializeView();
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;

            keyToolSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 3,
                FixedPanel = FixedPanel.None
            };
            Controls.Add(keyToolSplit);

            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            keyToolSplit.Panel1.Controls.Add(pnlLeft);

            TableLayoutPanel leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            pnlLeft.Controls.Add(leftLayout);

            Label lblLeftTitle = new Label
            {
                Text = "指紋輸入（可手動填寫或讀取本機）",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            leftLayout.Controls.Add(lblLeftTitle, 0, 0);

            Panel pnlInputScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            leftLayout.Controls.Add(pnlInputScroll, 0, 1);

            TableLayoutPanel inputTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 8,
                AutoSize = true
            };
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int rowHeight = 30;
            for (int i = 0; i < 8; i++)
                inputTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

            txtModuleId = CreateInputRow(inputTable, 0, "ModuleId", false);
            txtGuidKey = CreateInputRow(inputTable, 1, "GUID key（必填）", true);
            txtCpuProcessorId = CreateInputRow(inputTable, 2, "CPU ProcessorId (30)", false);
            txtSmbiosUuid = CreateInputRow(inputTable, 3, "SMBIOS UUID (20)", false);
            txtBiosSerial = CreateInputRow(inputTable, 4, "BIOS 序號 (15)", false);
            txtBaseBoardSerial = CreateInputRow(inputTable, 5, "主機板序號 (15)", false);
            txtVolumeSerial = CreateInputRow(inputTable, 6, "C: 卷序號 (10)", false);
            txtPhysicalNicMac = CreateInputRow(inputTable, 7, "實體網卡 MAC (10)", false);
            txtModuleId.Text = string.Empty;

            pnlInputScroll.Controls.Add(inputTable);

            btnDetectLocal = new Button
            {
                Text = "讀取本機",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                BackColor = Color.FromArgb(100, 149, 237),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 32
            };
            btnDetectLocal.FlatAppearance.BorderSize = 0;
            btnDetectLocal.Click += BtnDetectLocal_Click;
            leftLayout.Controls.Add(btnDetectLocal, 0, 2);

            Panel pnlRight = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            keyToolSplit.Panel2.Controls.Add(pnlRight);

            TableLayoutPanel rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                AutoSize = false
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            pnlRight.Controls.Add(rightLayout);

            Label lblRightTitle = new Label
            {
                Text = "金鑰結果",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rightLayout.Controls.Add(lblRightTitle, 0, 0);
            rightLayout.SetColumnSpan(lblRightTitle, 3);

            lblScoreSummary = new Label
            {
                Text = "總分：- / 門檻：60 / 底線：40",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DarkSlateGray
            };
            rightLayout.Controls.Add(lblScoreSummary, 0, 1);
            rightLayout.SetColumnSpan(lblScoreSummary, 3);

            Label lblVerifyKey = new Label
            {
                Text = "待驗證金鑰",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rightLayout.Controls.Add(lblVerifyKey, 0, 2);
            rightLayout.SetColumnSpan(lblVerifyKey, 3);

            txtVerifyKey = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F)
            };
            rightLayout.Controls.Add(txtVerifyKey, 0, 3);
            rightLayout.SetColumnSpan(txtVerifyKey, 3);

            btnGenerate = new Button
            {
                Text = "產生金鑰",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += BtnGenerate_Click;
            rightLayout.Controls.Add(btnGenerate, 0, 4);

            btnVerify = new Button
            {
                Text = "驗證金鑰",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                BackColor = Color.FromArgb(46, 139, 87),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnVerify.FlatAppearance.BorderSize = 0;
            btnVerify.Click += BtnVerify_Click;
            rightLayout.Controls.Add(btnVerify, 1, 4);

            btnCopy = new Button
            {
                Text = "複製金鑰",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += BtnCopy_Click;
            rightLayout.Controls.Add(btnCopy, 2, 4);

            txtKeyResult = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10F),
                BackColor = Color.White
            };
            rightLayout.Controls.Add(txtKeyResult, 0, 5);
            rightLayout.SetColumnSpan(txtKeyResult, 3);

            lblStatus = new Label
            {
                Text = "就緒 - 請輸入指紋值或按「讀取本機」自動偵測",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F),
                ForeColor = Color.DarkGreen
            };
            rightLayout.Controls.Add(lblStatus, 0, 6);
            rightLayout.SetColumnSpan(lblStatus, 3);
        }

        private TextBox CreateInputRow(TableLayoutPanel table, int rowIndex, string labelText, bool bold)
        {
            Label lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 8.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };
            table.Controls.Add(lbl, 0, rowIndex);

            TextBox txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F)
            };
            table.Controls.Add(txt, 1, rowIndex);

            return txt;
        }

        public void RefreshView(bool showErrorMessage)
        {
        }

        private void BtnDetectLocal_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "正在讀取本機硬體指紋...";
                lblStatus.ForeColor = Color.Blue;
                Application.DoEvents();

                KeyToolSnapshot local = keyToolService.QueryLocalMachineFingerprints();

                txtCpuProcessorId.Text = local.CpuProcessorId;
                txtSmbiosUuid.Text = local.SmbiosUuid;
                txtBiosSerial.Text = local.BiosSerialNumber;
                txtBaseBoardSerial.Text = local.BaseBoardSerialNumber;
                txtVolumeSerial.Text = local.SystemDriveVolumeSerial;
                txtPhysicalNicMac.Text = local.PhysicalNicMac;

                if (!string.IsNullOrWhiteSpace(local.ErrorMessage))
                {
                    lblStatus.Text = $"[失敗] {local.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                }
                else
                {
                    lblStatus.Text = "[成功] 已讀取本機指紋，可手動修改後再產生金鑰";
                    lblStatus.ForeColor = Color.DarkGreen;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "[失敗] 讀取本機指紋時發生錯誤";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"讀取失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "正在計算金鑰...";
                lblStatus.ForeColor = Color.Blue;
                Application.DoEvents();

                KeyToolSnapshot result = keyToolService.ComputeKeyFromValues(
                    txtModuleId.Text,
                    txtGuidKey.Text,
                    txtCpuProcessorId.Text,
                    txtSmbiosUuid.Text,
                    txtBiosSerial.Text,
                    txtBaseBoardSerial.Text,
                    txtVolumeSerial.Text,
                    txtPhysicalNicMac.Text);

                lblScoreSummary.Text = $"總分：{result.TotalScore} / 門檻：{result.PassThreshold} / 底線：{result.RebindThreshold}";

                if (result.IsPassedByScore)
                    lblScoreSummary.ForeColor = Color.DarkGreen;
                else if (result.IsRebindEligible)
                    lblScoreSummary.ForeColor = Color.DarkOrange;
                else
                    lblScoreSummary.ForeColor = Color.Red;

                string rawHash = result.KeyResult ?? string.Empty;
                string guid = (txtGuidKey.Text ?? string.Empty).Trim();
                string key = (!string.IsNullOrWhiteSpace(guid) && !string.IsNullOrWhiteSpace(rawHash))
                    ? $"{guid}:{rawHash}"
                    : rawHash;
                currentKey = key;
                txtVerifyKey.Text = key;

                if (string.IsNullOrWhiteSpace(key))
                {
                    txtKeyResult.Text = string.Empty;
                }
                else
                {
                    txtKeyResult.Text = BuildKeyDetailsText(
                        "授權金鑰（完整格式，可直接貼入 HN-Studio）",
                        key,
                        "產生結果",
                        result.IsPassedByScore
                            ? "有效指紋分數達標，可作為授權金鑰。"
                            : result.IsRebindEligible
                                ? "有效指紋分數未達門檻，但已達重新綁定底線。"
                                : "有效指紋分數不足，這把金鑰不應作為正式授權。",
                        result);
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    lblStatus.Text = $"[失敗] {result.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show(result.ErrorMessage, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    lblStatus.Text = "[成功] 金鑰已產生";
                    lblStatus.ForeColor = Color.DarkGreen;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "[失敗] 產生金鑰時發生錯誤";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"產生金鑰時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnVerify_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "正在驗證金鑰...";
                lblStatus.ForeColor = Color.Blue;
                Application.DoEvents();

                KeyToolSnapshot result = keyToolService.VerifyKeyAgainstValues(
                    txtModuleId.Text,
                    txtVerifyKey.Text,
                    txtCpuProcessorId.Text,
                    txtSmbiosUuid.Text,
                    txtBiosSerial.Text,
                    txtBaseBoardSerial.Text,
                    txtVolumeSerial.Text,
                    txtPhysicalNicMac.Text);

                lblScoreSummary.Text = $"總分：{result.TotalScore} / 門檻：{result.PassThreshold} / 底線：{result.RebindThreshold}";
                if (result.IsActivationReady)
                    lblScoreSummary.ForeColor = Color.DarkGreen;
                else if (result.IsKeyMatched)
                    lblScoreSummary.ForeColor = result.IsRebindEligible ? Color.DarkOrange : Color.Red;
                else
                    lblScoreSummary.ForeColor = Color.Red;

                string verificationState = !result.IsKeyFormatValid
                    ? "金鑰格式無效"
                    : result.IsActivationReady
                        ? "金鑰正確，且目前欄位可通過授權"
                        : result.IsKeyMatched
                            ? "金鑰正確，但目前有效分數不足"
                            : "金鑰不正確，無法由目前欄位重算得到";

                txtKeyResult.Text = BuildKeyDetailsText(
                    "待驗證金鑰",
                    result.InputLicenseKey,
                    "驗證結果",
                    verificationState,
                    result);

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    lblStatus.Text = $"[失敗] {result.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                }
                else
                {
                    lblStatus.Text = "[成功] 金鑰驗證通過";
                    lblStatus.ForeColor = Color.DarkGreen;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "[失敗] 驗證金鑰時發生錯誤";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"驗證金鑰時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            try
            {
                string text = currentKey ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    lblStatus.Text = "[失敗] 金鑰為空";
                    lblStatus.ForeColor = Color.Red;
                    return;
                }

                Clipboard.SetText(text);
                lblStatus.Text = "[成功] 已複製到剪貼簿";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "[失敗] 複製失敗";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"複製失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildKeyDetailsText(
            string keyTitle,
            string keyText,
            string summaryTitle,
            string summaryText,
            KeyToolSnapshot result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(keyTitle);
            sb.AppendLine(keyText);
            sb.AppendLine();
            sb.AppendLine($"ModuleId：{(string.IsNullOrWhiteSpace(result.ModuleId) ? "一般金鑰模式" : result.ModuleId)}");
            sb.AppendLine();
            sb.AppendLine(summaryTitle);
            sb.AppendLine(summaryText);
            if (!string.IsNullOrWhiteSpace(result.KeyResult))
            {
                sb.AppendLine($"重算雜湊：{result.KeyResult}");
            }

            sb.AppendLine();
            sb.AppendLine("--- 加權明細 ---");
            foreach (var fp in result.Fingerprints)
            {
                string status = (!fp.IsAvailable) ? "缺失" : (!fp.IsValid) ? "無效值" : "有效";
                string score = (fp.IsAvailable && fp.IsValid) ? $"+{fp.Weight}" : "  0";
                sb.AppendLine($"  {score}  {fp.Label}: {status}  {fp.Value}");
            }

            return sb.ToString();
        }
    }
}
