using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NetToolTestArea.Model;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    /// <summary>
    /// 抓取單項畫面（View）
    /// </summary>
    public sealed class CaptureSingleView : UserControl
    {
        private readonly CaptureSingleService captureSingleService;

        private DataGridView dgv;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnActivate;
        private Label lblStatus;

        public CaptureSingleView(CaptureSingleService captureSingleService)
        {
            this.captureSingleService = captureSingleService;
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
                Text = "抓取選單項",
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
            try
            {
                lblStatus.Text = "查找 InSpec 視窗中...";
                Application.DoEvents();

                IntPtr inspecWindow = captureSingleService.FindInSpecWindow();
                if (inspecWindow == IntPtr.Zero)
                {
                    MessageBox.Show("未找到 InSpec 視窗。\r\n\r\n請確認：\r\n1. InSpec 應用程式已開啟\r\n2. 視窗標題包含 \"InSpec\"", "未找到視窗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "未找到視窗";
                    return;
                }

                lblStatus.Text = "正在讀取選單項...";
                Application.DoEvents();

                List<MenuItemInfo> menuItems = captureSingleService.QueryMenuItems(inspecWindow);
                if (menuItems.Count == 0)
                {
                    MessageBox.Show("未找到選單項。\r\n\r\n可能原因：\r\n1. InSpec 視窗沒有選單列\r\n2. 選單列無法讀取", "未找到選單項", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "未找到選單項";
                    return;
                }

                UpdateGrid(menuItems);
                lblStatus.Text = $"已讀取 {menuItems.Count} 個選單項";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"讀取選單項時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "讀取失敗";
            }
        }

        private void UpdateGrid(List<MenuItemInfo> menuItems)
        {
            dgv.Rows.Clear();

            foreach (MenuItemInfo item in menuItems)
            {
                dgv.Rows.Add(
                    "-",
                    item.MenuPath,
                    "-",
                    "-",
                    $"{item.CommandId} (0x{item.CommandId:X})"
                );
            }
        }

        private void PerformSearch()
        {
            string searchText = (txtSearch.Text ?? string.Empty).ToLower();
            if (string.IsNullOrEmpty(searchText))
            {
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


