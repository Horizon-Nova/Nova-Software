using System;
using System.Drawing;
using System.Windows.Forms;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    public sealed class IsolatedStorageView : UserControl
    {
        private readonly IsolatedStorageService isolatedStorageService;

        private TextBox txtCurrentTargetPath;
        private TextBox txtInputContent;
        private TextBox txtResult;
        private Label lblStatus;
        private Button btnSaveCurrentTarget;
        private Button btnLoadCurrentTarget;
        private Button btnDeleteCurrentTarget;
        private Button btnAppendInput;
        private Button btnRefreshState;

        public IsolatedStorageView(IsolatedStorageService isolatedStorageService)
        {
            this.isolatedStorageService = isolatedStorageService;
            InitializeView();
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 7,
                Padding = new Padding(10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(layout);

            Label lblTitle = new Label
            {
                Text = "IsolatedStorage 測試",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(lblTitle, 0, 0);
            layout.SetColumnSpan(lblTitle, 4);

            Label lblCurrentTarget = new Label
            {
                Text = "CurrentTargetPath",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F)
            };
            layout.Controls.Add(lblCurrentTarget, 0, 1);

            txtCurrentTargetPath = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F)
            };
            layout.Controls.Add(txtCurrentTargetPath, 1, 1);

            btnSaveCurrentTarget = new Button
            {
                Text = "保存 Target",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveCurrentTarget.FlatAppearance.BorderSize = 0;
            btnSaveCurrentTarget.Click += BtnSaveCurrentTarget_Click;
            layout.Controls.Add(btnSaveCurrentTarget, 2, 1);

            btnLoadCurrentTarget = new Button
            {
                Text = "讀取 Target",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(46, 139, 87),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLoadCurrentTarget.FlatAppearance.BorderSize = 0;
            btnLoadCurrentTarget.Click += BtnLoadCurrentTarget_Click;
            layout.Controls.Add(btnLoadCurrentTarget, 3, 1);

            Label lblInputContent = new Label
            {
                Text = "Input 內容",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F)
            };
            layout.Controls.Add(lblInputContent, 0, 2);

            txtInputContent = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                Text = "這是測試寫入內容。"
            };
            layout.Controls.Add(txtInputContent, 1, 2);

            btnDeleteCurrentTarget = new Button
            {
                Text = "刪除 Target",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(205, 92, 92),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDeleteCurrentTarget.FlatAppearance.BorderSize = 0;
            btnDeleteCurrentTarget.Click += BtnDeleteCurrentTarget_Click;
            layout.Controls.Add(btnDeleteCurrentTarget, 2, 2);

            btnRefreshState = new Button
            {
                Text = "刷新狀態",
                Dock = DockStyle.Fill,
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRefreshState.FlatAppearance.BorderSize = 0;
            btnRefreshState.Click += BtnRefreshState_Click;
            layout.Controls.Add(btnRefreshState, 3, 2);

            Label lblHint = new Label
            {
                Text = "先保存 CurrentTargetPath，再用附加寫入驗證程式重開後是否仍可讀回。",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(lblHint, 0, 3);
            layout.SetColumnSpan(lblHint, 4);

            btnAppendInput = new Button
            {
                Text = "附加寫入 Target",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(123, 104, 238),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAppendInput.FlatAppearance.BorderSize = 0;
            btnAppendInput.Click += BtnAppendInput_Click;
            layout.Controls.Add(btnAppendInput, 0, 4);
            layout.SetColumnSpan(btnAppendInput, 4);

            txtResult = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White
            };
            layout.Controls.Add(txtResult, 0, 5);
            layout.SetColumnSpan(txtResult, 4);

            lblStatus = new Label
            {
                Text = "待命",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F),
                ForeColor = Color.DarkGreen
            };
            layout.Controls.Add(lblStatus, 0, 6);
            layout.SetColumnSpan(lblStatus, 4);
        }

        public void RefreshView()
        {
            try
            {
                txtResult.Text = isolatedStorageService.QueryDiagnosticText();
                if (string.IsNullOrWhiteSpace(txtCurrentTargetPath.Text))
                {
                    txtCurrentTargetPath.Text = isolatedStorageService.LoadCurrentTargetPath();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"刷新失敗: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnSaveCurrentTarget_Click(object sender, EventArgs e)
        {
            try
            {
                isolatedStorageService.SaveCurrentTargetPath(txtCurrentTargetPath.Text);
                txtResult.Text = isolatedStorageService.QueryDiagnosticText();
                lblStatus.Text = "CurrentTargetPath 已保存到 IsolatedStorage。";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"保存失敗: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnLoadCurrentTarget_Click(object sender, EventArgs e)
        {
            try
            {
                txtCurrentTargetPath.Text = isolatedStorageService.LoadCurrentTargetPath();
                txtResult.Text = isolatedStorageService.QueryDiagnosticText();
                lblStatus.Text = "已從 IsolatedStorage 讀回 CurrentTargetPath。";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"讀取失敗: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnDeleteCurrentTarget_Click(object sender, EventArgs e)
        {
            try
            {
                isolatedStorageService.DeleteCurrentTargetPath();
                txtResult.Text = isolatedStorageService.QueryDiagnosticText();
                lblStatus.Text = "已刪除 IsolatedStorage 內的 CurrentTargetPath。";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"刪除失敗: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnAppendInput_Click(object sender, EventArgs e)
        {
            try
            {
                string targetPath = isolatedStorageService.AppendInputToCurrentTarget(txtInputContent.Text);
                txtResult.Text = isolatedStorageService.QueryDiagnosticText()
                    + Environment.NewLine
                    + "AppendTarget: "
                    + targetPath;
                lblStatus.Text = "已將 Input 內容附加到 CurrentTarget 指向的檔案。";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"附加寫入失敗: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnRefreshState_Click(object sender, EventArgs e)
        {
            RefreshView();
            lblStatus.Text = "狀態已刷新。";
            lblStatus.ForeColor = Color.DarkGreen;
        }
    }
}
