using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NetToolTestArea.Model;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    /// <summary>
    /// 抓取視窗控件畫面（View）
    /// </summary>
    public sealed class CaptureWindowView : UserControl
    {
        private readonly CaptureWindowService captureWindowService;
        private readonly WindowApiService windowApiService;

        private DataGridView dgv;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnActivate;
        private Label lblStatus;

        public CaptureWindowView(
            CaptureWindowService captureWindowService,
            WindowApiService windowApiService)
        {
            this.captureWindowService = captureWindowService;
            this.windowApiService = windowApiService;
            InitializeView();
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;

            Panel root = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                BackColor = Color.White
            };
            Controls.Add(root);

            TableLayoutPanel pnlSearch = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(3),
                ColumnCount = 5,
                RowCount = 1,
                AutoSize = false
            };
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.Controls.Add(pnlSearch);

            Label lblSearch = new Label
            {
                Text = "查詢：",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold),
                AutoSize = false
            };
            pnlSearch.Controls.Add(lblSearch, 0, 0);

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                Margin = new Padding(0, 2, 5, 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformSearch(); };
            pnlSearch.Controls.Add(txtSearch, 1, 0);

            btnSearch = new Button
            {
                Text = "查詢",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                Margin = new Padding(0, 2, 5, 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            btnSearch.Click += (s, e) => PerformSearch();
            pnlSearch.Controls.Add(btnSearch, 2, 0);

            btnActivate = new Button
            {
                Text = "抓取視窗控件",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei", 9F),
                BackColor = Color.LightGray,
                Margin = new Padding(0, 2, 5, 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            btnActivate.Click += BtnActivate_Click;
            pnlSearch.Controls.Add(btnActivate, 3, 0);

            lblStatus = new Label
            {
                Text = "就緒",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei", 9F),
                ForeColor = Color.DarkGreen,
                AutoSize = false
            };
            pnlSearch.Controls.Add(lblStatus, 4, 0);

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Microsoft JhengHei", 9F),
                AllowUserToAddRows = false,
                Margin = new Padding(0, 5, 0, 0)
            };
            SetupDataGridView(dgv);
            root.Controls.Add(dgv);
        }

        public void RefreshView()
        {
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            if (!captureWindowService.IsActive)
            {
                EnableCaptureWindow();
                return;
            }

            DisableCaptureWindow();
        }

        private void EnableCaptureWindow()
        {
            btnActivate.Text = "停用抓取模式";
            btnActivate.BackColor = Color.LightGreen;
            lblStatus.Text = "抓取視窗控件模式已啟用 - 請點擊目標視窗";

            captureWindowService.Enable((info) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<ControlInfo>(OnWindowControlSelected), info);
                }
                else
                {
                    OnWindowControlSelected(info);
                }
            });
        }

        private void DisableCaptureWindow()
        {
            captureWindowService.Disable();
            btnActivate.Text = "抓取視窗控件";
            btnActivate.BackColor = Color.LightGray;
            lblStatus.Text = "抓取視窗控件模式已停用";
        }

        private void OnWindowControlSelected(ControlInfo info)
        {
            try
            {
                DisableCaptureWindow();

                IntPtr targetWindow = windowApiService.QueryTopLevelWindow(info.Handle);
                if (targetWindow == IntPtr.Zero)
                {
                    targetWindow = info.Handle;
                }

                lblStatus.Text = "正在讀取控件...";
                Application.DoEvents();

                List<ControlInfo> allControls = captureWindowService.QueryAllControls(targetWindow);
                if (allControls.Count == 0)
                {
                    MessageBox.Show("未找到控件。\r\n\r\n可能原因：\r\n1. 視窗沒有子控件\r\n2. 控件無法讀取", "未找到控件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "未找到控件";
                    return;
                }

                UpdateDetailsGrid(allControls, dgv);
                lblStatus.Text = $"已讀取 {allControls.Count} 個控件";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"讀取控件時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "讀取失敗";
            }
        }

        private static void UpdateDetailsGrid(List<ControlInfo> controls, DataGridView dgv)
        {
            dgv.Rows.Clear();

            for (int i = 0; i < (controls?.Count ?? 0); i++)
            {
                ControlInfo control = controls[i];
                dgv.Rows.Add(
                    $"0x{control.Handle.ToInt64():X}",
                    control.WindowTitle,
                    control.ClassName,
                    control.Text,
                    control.CommandId >= 0 ? $"{control.CommandId} (0x{control.CommandId:X})" : "-"
                );
            }
        }

        private void PerformSearch()
        {
            string searchText = (txtSearch.Text ?? string.Empty).ToLower();
            if (string.IsNullOrEmpty(searchText))
            {
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    row.Visible = true;
                }
                return;
            }

            foreach (DataGridViewRow row in dgv.Rows)
            {
                bool visible = false;
                foreach (DataGridViewCell cell in row.Cells)
                {
                    if (cell.Value != null && cell.Value.ToString().ToLower().Contains(searchText))
                    {
                        visible = true;
                        break;
                    }
                }
                row.Visible = visible;
            }
        }

        private void SetupDataGridView(DataGridView dgv)
        {
            dgv.Columns.Clear();
            dgv.Columns.Add("Handle", "句柄ID");
            dgv.Columns.Add("WindowTitle", "視窗標題");
            dgv.Columns.Add("ClassName", "類名");
            dgv.Columns.Add("Text", "文字內容");
            dgv.Columns.Add("CommandId", "WM_COMMAND ID");

            dgv.Columns["Handle"].Width = 120;
            dgv.Columns["WindowTitle"].Width = 200;
            dgv.Columns["ClassName"].Width = 150;
            dgv.Columns["Text"].Width = 300;
            dgv.Columns["CommandId"].Width = 120;

            dgv.BackgroundColor = Color.White;
            dgv.GridColor = Color.FromArgb(220, 220, 220);
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.EnableHeadersVisualStyles = true;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 240, 240);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 220, 255);
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgv.DefaultCellStyle.Padding = new Padding(3);
        }
    }
}


