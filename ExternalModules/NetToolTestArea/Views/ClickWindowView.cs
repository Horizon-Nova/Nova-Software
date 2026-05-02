using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NetToolTestArea.Model;
using NetToolTestArea.Services;

namespace NetToolTestArea.Views
{
    /// <summary>
    /// 點擊視窗畫面（View）
    /// </summary>
    public sealed class ClickWindowView : UserControl
    {
        private readonly ClickWindowService clickWindowService;
        private readonly WindowApiService windowApiService;

        private SplitContainer split;

        private ListBox lstButtons;
        private DataGridView dgvDetails;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnActivate;
        private Label lblStatus;

        private readonly Dictionary<int, IntPtr> buttonHandles = new Dictionary<int, IntPtr>();
        private List<ControlInfo> lastAllControls = new List<ControlInfo>();
        private bool splitterInitialized = false;

        public ClickWindowView(
            ClickWindowService clickWindowService,
            WindowApiService windowApiService)
        {
            this.clickWindowService = clickWindowService;
            this.windowApiService = windowApiService;

            InitializeView();
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;

            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 3,
                FixedPanel = FixedPanel.None
            };
            Controls.Add(split);

            // 左側：檢測按鈕清單
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            split.Panel1.Controls.Add(pnlLeft);

            Label lblButtonList = new Label
            {
                Text = "檢測按鈕清單：",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Microsoft JhengHei", 9F, FontStyle.Bold),
                Padding = new Padding(0, 3, 0, 3)
            };
            pnlLeft.Controls.Add(lblButtonList);

            lstButtons = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None
            };
            lstButtons.SelectedIndexChanged += (s, e) => { };
            lstButtons.DoubleClick += LstButtons_DoubleClick;
            pnlLeft.Controls.Add(lstButtons);

            // 右側：詳細資訊 + 查詢
            Panel pnlRight = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            split.Panel2.Controls.Add(pnlRight);

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
            pnlRight.Controls.Add(pnlSearch);

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
                Text = "啟用點擊視窗",
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

            dgvDetails = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Microsoft JhengHei", 9F),
                AllowUserToAddRows = false,
                Margin = new Padding(0, 5, 0, 0)
            };
            SetupDataGridView(dgvDetails);
            AddIndexColumnIfMissing(dgvDetails);
            pnlRight.Controls.Add(dgvDetails);

            VisibleChanged += (s, e) =>
            {
                if (Visible)
                {
                    EnsureSplitter50();
                }
            };
        }

        public void RefreshView()
        {
            EnsureSplitter50();
        }

        private void EnsureSplitter50()
        {
            if (splitterInitialized) return;
            if (split == null) return;
            if (split.Width <= 0) return;
            split.SplitterDistance = split.Width / 2;
            splitterInitialized = true;
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            if (!clickWindowService.IsActive)
            {
                EnableClickWindow();
                return;
            }

            DisableClickWindow();
        }

        private void EnableClickWindow()
        {
            clickWindowService.Enable(OnWindowAllSelected);
            btnActivate.Text = "停用點擊視窗";
            btnActivate.BackColor = Color.LightGreen;
            lblStatus.Text = "點擊視窗模式已啟用 - 請點擊目標視窗";
        }

        private void DisableClickWindow()
        {
            clickWindowService.Disable();
            btnActivate.Text = "啟用點擊視窗";
            btnActivate.BackColor = Color.LightGray;
            lblStatus.Text = "點擊視窗模式已停用";
        }

        private void OnWindowAllSelected(ControlInfo info)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ControlInfo>(OnWindowAllSelected), info);
                return;
            }

            IntPtr topLevelWindow = windowApiService.QueryTopLevelWindow(info.Handle);
            IntPtr targetWindow = topLevelWindow != IntPtr.Zero ? topLevelWindow : info.Handle;

            ControlInfo rootInfo = clickWindowService.QueryControlInfo(targetWindow);
            List<ControlInfo> allControls = clickWindowService.QueryAllControls(targetWindow) ?? new List<ControlInfo>();
            allControls = PrependRootControl(rootInfo, allControls);
            lastAllControls = allControls;

            if (topLevelWindow != IntPtr.Zero)
            {
                ControlInfo topLevelInfo = clickWindowService.QueryControlInfo(topLevelWindow);
                info = topLevelInfo;
            }

            List<ControlInfo> clickableButtons = FilterClickableControls(lastAllControls);
            UpdateButtonList(clickableButtons);
            UpdateDetailsGrid(lastAllControls, dgvDetails);

            lblStatus.Text = $"已讀取視窗全部內容 ({lastAllControls.Count} 個控制項)";
            DisableClickWindow();
        }

        private static List<ControlInfo> PrependRootControl(ControlInfo rootInfo, List<ControlInfo> controls)
        {
            List<ControlInfo> result = new List<ControlInfo>();
            if (rootInfo != null)
            {
                result.Add(rootInfo);
            }
            if (controls != null && controls.Count > 0)
            {
                result.AddRange(controls);
            }
            return result;
        }

        private List<ControlInfo> FilterClickableControls(List<ControlInfo> controls)
        {
            return controls == null
                ? new List<ControlInfo>()
                : controls.Where(c => c != null && windowApiService.IsClickableClassName(c.ClassName)).ToList();
        }

        private void UpdateButtonList(List<ControlInfo> controls)
        {
            lstButtons.Items.Clear();
            buttonHandles.Clear();

            for (int i = 0; i < (controls?.Count ?? 0); i++)
            {
                ControlInfo control = controls[i];
                string displayText = $"#{i + 1} - 0x{control.Handle.ToInt64():X} - {control.ClassName}";
                if (!string.IsNullOrEmpty(control.Text))
                {
                    displayText += $" - {control.Text}";
                }
                lstButtons.Items.Add(displayText);
                buttonHandles[i] = control.Handle;
            }
        }

        private static void UpdateDetailsGrid(List<ControlInfo> controls, DataGridView dgv)
        {
            dgv.Rows.Clear();

            bool hasIndex = dgv.Columns.Contains("Index");
            for (int i = 0; i < (controls?.Count ?? 0); i++)
            {
                ControlInfo control = controls[i];
                if (hasIndex)
                {
                    dgv.Rows.Add(
                        $"#{i + 1}",
                        $"0x{control.Handle.ToInt64():X}",
                        control.WindowTitle,
                        control.ClassName,
                        control.Text,
                        control.CommandId >= 0 ? $"{control.CommandId} (0x{control.CommandId:X})" : "-"
                    );
                    continue;
                }

                dgv.Rows.Add(
                    $"0x{control.Handle.ToInt64():X}",
                    control.WindowTitle,
                    control.ClassName,
                    control.Text,
                    control.CommandId >= 0 ? $"{control.CommandId} (0x{control.CommandId:X})" : "-"
                );
            }
        }

        private static void AddIndexColumnIfMissing(DataGridView dgv)
        {
            if (dgv == null) return;
            if (dgv.Columns.Contains("Index")) return;

            DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn
            {
                Name = "Index",
                HeaderText = "#編號",
                Width = 70,
                ReadOnly = true
            };
            dgv.Columns.Insert(0, col);
        }

        private void PerformSearch()
        {
            string searchText = (txtSearch.Text ?? string.Empty).ToLower();
            List<ControlInfo> source = lastAllControls ?? new List<ControlInfo>();
            List<ControlInfo> result = string.IsNullOrEmpty(searchText)
                ? source
                : source.Where(c => MatchesSearch(c, searchText)).ToList();

            UpdateDetailsGrid(result, dgvDetails);
        }

        private static bool MatchesSearch(ControlInfo control, string searchTextLower)
        {
            if (control == null) return false;
            if (string.IsNullOrEmpty(searchTextLower)) return true;

            string handleHex = control.Handle.ToInt64().ToString("X").ToLower();
            string windowTitle = (control.WindowTitle ?? "").ToLower();
            string className = (control.ClassName ?? "").ToLower();
            string text = (control.Text ?? "").ToLower();
            string commandId = control.CommandId >= 0 ? control.CommandId.ToString() : "";

            return handleHex.Contains(searchTextLower)
                || windowTitle.Contains(searchTextLower)
                || className.Contains(searchTextLower)
                || text.Contains(searchTextLower)
                || (!string.IsNullOrEmpty(commandId) && commandId.Contains(searchTextLower));
        }

        private void LstButtons_DoubleClick(object sender, EventArgs e)
        {
            if (lstButtons.SelectedIndex < 0) return;
            if (!buttonHandles.TryGetValue(lstButtons.SelectedIndex, out IntPtr handle)) return;

            TestButton(handle);
        }

        private void TestButton(IntPtr handle)
        {
            try
            {
                string className = windowApiService.QueryControlClassName(handle);
                if (className == "ToolbarWindow32" || !windowApiService.IsButtonClassName(className))
                {
                    windowApiService.ClickToolbarButton(handle);
                }
                else
                {
                    windowApiService.ClickStandardButton(handle);
                }
                MessageBox.Show($"已點擊控制項 0x{handle.ToInt64():X}", "測試成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"點擊控制項失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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


