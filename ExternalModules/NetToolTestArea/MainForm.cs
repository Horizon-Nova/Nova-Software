using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NetToolTestArea.Core;
using NetToolTestArea.Services;
using NetToolTestArea.Model;
using NetToolTestArea.Utility;
using NetToolTestArea.Views;

namespace NetToolTestArea
{
    public partial class MainForm : Form
    {
        private Button btnNavClickMode;
        private Button btnNavClickWindow;
        private Button btnNavCaptureSingle;
        private Button btnNavCaptureWindow;
        private Button btnNavFunctionAnalysis;
        private Button btnNavKeyTool;
        private Button btnNavIsolatedStorage;
        private Panel pnlNavigation;
        private Panel pnlContent;
        private ClickModeView clickModeView;
        private ClickWindowView clickWindowView;
        private CaptureSingleView captureSingleView;
        private CaptureWindowView captureWindowView;
        private FunctionAnalysisView functionAnalysisView;
        private ServiceContainer services;
        private ClickModeService clickModeService;
        private ClickWindowService clickWindowService;
        private CaptureSingleService captureSingleService;
        private CaptureWindowService captureWindowService;
        private FunctionAnalysisService functionAnalysisService;
        private WindowApiService windowApiService;
        private KeyToolService keyToolService;
        private IsolatedStorageService isolatedStorageService;
        private KeyToolView keyToolView;
        private IsolatedStorageView isolatedStorageView;
        private string currentView = "ClickMode";

        public MainForm()
        {
            services = new ServiceContainer();
            clickModeService = services.ClickModeService;
            clickWindowService = services.ClickWindowService;
            captureSingleService = services.CaptureSingleService;
            captureWindowService = services.CaptureWindowService;
            functionAnalysisService = services.FunctionAnalysisService;
            windowApiService = services.WindowApiService;
            keyToolService = services.KeyToolService;
            isolatedStorageService = services.IsolatedStorageService;
            
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            this.Text = "NetTool 測試區域";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 500);
            this.MaximizeBox = false;

            SplitContainer mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 180,
                SplitterWidth = 3,
                FixedPanel = FixedPanel.Panel1
            };
            this.Controls.Add(mainSplit);

            CreateNavigationPanel(mainSplit.Panel1);
            
            CreateContentArea(mainSplit.Panel2);
            
            clickModeView = new ClickModeView(clickModeService, clickWindowService, windowApiService) { Visible = false };
            clickWindowView = new ClickWindowView(clickWindowService, windowApiService) { Visible = false };
            captureSingleView = new CaptureSingleView(captureSingleService) { Visible = false };
            captureWindowView = new CaptureWindowView(captureWindowService, windowApiService) { Visible = false };
            functionAnalysisView = new FunctionAnalysisView(functionAnalysisService) { Visible = false };
            keyToolView = new KeyToolView(keyToolService) { Visible = false };
            isolatedStorageView = new IsolatedStorageView(isolatedStorageService) { Visible = false };

            pnlContent.Controls.Add(clickModeView);
            pnlContent.Controls.Add(clickWindowView);
            pnlContent.Controls.Add(captureSingleView);
            pnlContent.Controls.Add(captureWindowView);
            pnlContent.Controls.Add(functionAnalysisView);
            pnlContent.Controls.Add(keyToolView);
            pnlContent.Controls.Add(isolatedStorageView);
            
            ShowView("ClickMode");
        }
        private void CreateNavigationPanel(Panel container)
        {
            pnlNavigation = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5, 5, 5, 5)
            };
            container.Controls.Add(pnlNavigation);
            
            btnNavClickMode = new Button
            {
                Text = "點擊模式",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightBlue,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavClickMode.FlatAppearance.BorderSize = 0;
            btnNavClickMode.FlatAppearance.MouseOverBackColor = Color.FromArgb(173, 216, 230);
            btnNavClickMode.Click += (s, e) => ShowView("ClickMode");
            pnlNavigation.Controls.Add(btnNavClickMode);
            
            btnNavClickWindow = new Button
            {
                Text = "點擊視窗",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavClickWindow.FlatAppearance.BorderSize = 0;
            btnNavClickWindow.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavClickWindow.Click += (s, e) => ShowView("ClickWindow");
            pnlNavigation.Controls.Add(btnNavClickWindow);
            
            btnNavCaptureSingle = new Button
            {
                Text = "抓取單項",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavCaptureSingle.FlatAppearance.BorderSize = 0;
            btnNavCaptureSingle.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavCaptureSingle.Click += (s, e) => ShowView("CaptureSingle");
            pnlNavigation.Controls.Add(btnNavCaptureSingle);
            
            btnNavCaptureWindow = new Button
            {
                Text = "抓取視窗",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(3)
            };
            btnNavCaptureWindow.FlatAppearance.BorderSize = 0;
            btnNavCaptureWindow.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavCaptureWindow.Click += (s, e) => ShowView("CaptureWindow");
            pnlNavigation.Controls.Add(btnNavCaptureWindow);
            
            btnNavFunctionAnalysis = new Button
            {
                Text = "函數分析",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavFunctionAnalysis.FlatAppearance.BorderSize = 0;
            btnNavFunctionAnalysis.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavFunctionAnalysis.Click += (s, e) => ShowView("FunctionAnalysis");
            pnlNavigation.Controls.Add(btnNavFunctionAnalysis);
            
            btnNavKeyTool = new Button
            {
                Text = "金鑰工具",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavKeyTool.FlatAppearance.BorderSize = 0;
            btnNavKeyTool.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavKeyTool.Click += (s, e) => ShowView("KeyTool");
            pnlNavigation.Controls.Add(btnNavKeyTool);

            btnNavIsolatedStorage = new Button
            {
                Text = "IsolatedStorage",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Microsoft JhengHei", 10F, FontStyle.Bold),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(3)
            };
            btnNavIsolatedStorage.FlatAppearance.BorderSize = 0;
            btnNavIsolatedStorage.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 211, 211);
            btnNavIsolatedStorage.Click += (s, e) => ShowView("IsolatedStorage");
            pnlNavigation.Controls.Add(btnNavIsolatedStorage);
        }
        private void CreateContentArea(Panel container)
        {
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };
            container.Controls.Add(pnlContent);
        }
        private void ShowView(string viewName)
        {
            if (clickModeView != null) clickModeView.Visible = false;
            if (clickWindowView != null) clickWindowView.Visible = false;
            if (captureSingleView != null) captureSingleView.Visible = false;
            if (captureWindowView != null) captureWindowView.Visible = false;
            if (functionAnalysisView != null) functionAnalysisView.Visible = false;
            if (keyToolView != null)
            {
                keyToolView.Visible = false;
            }
            if (isolatedStorageView != null)
            {
                isolatedStorageView.Visible = false;
            }
            
            btnNavClickMode.BackColor = Color.LightGray;
            btnNavClickWindow.BackColor = Color.LightGray;
            btnNavCaptureSingle.BackColor = Color.LightGray;
            btnNavCaptureWindow.BackColor = Color.LightGray;
            btnNavFunctionAnalysis.BackColor = Color.LightGray;
            if (btnNavKeyTool != null)
            {
                btnNavKeyTool.BackColor = Color.LightGray;
            }
            if (btnNavIsolatedStorage != null)
            {
                btnNavIsolatedStorage.BackColor = Color.LightGray;
            }
            
            currentView = viewName;
            switch (viewName)
            {
                case "ClickMode":
                    if (clickModeView != null)
                    {
                        clickModeView.Visible = true;
                        clickModeView.RefreshView();
                    }
                    btnNavClickMode.BackColor = Color.LightBlue;
                    break;
                case "ClickWindow":
                    if (clickWindowView != null)
                    {
                        clickWindowView.Visible = true;
                        clickWindowView.RefreshView();
                    }
                    btnNavClickWindow.BackColor = Color.LightBlue;
                    break;
                case "CaptureSingle":
                    if (captureSingleView != null)
                    {
                        captureSingleView.Visible = true;
                        captureSingleView.RefreshView();
                    }
                    btnNavCaptureSingle.BackColor = Color.LightBlue;
                    break;
                case "CaptureWindow":
                    if (captureWindowView != null)
                    {
                        captureWindowView.Visible = true;
                        captureWindowView.RefreshView();
                    }
                    btnNavCaptureWindow.BackColor = Color.LightBlue;
                    break;
                case "FunctionAnalysis":
                    if (functionAnalysisView != null)
                    {
                        functionAnalysisView.Visible = true;
                        functionAnalysisView.RefreshView();
                    }
                    btnNavFunctionAnalysis.BackColor = Color.LightBlue;
                    break;
                case "KeyTool":
                    if (btnNavKeyTool != null)
                    {
                        btnNavKeyTool.BackColor = Color.LightBlue;
                    }
                    if (keyToolView != null)
                    {
                        keyToolView.Visible = true;
                        keyToolView.RefreshView(false);
                    }
                    break;
                case "IsolatedStorage":
                    if (btnNavIsolatedStorage != null)
                    {
                        btnNavIsolatedStorage.BackColor = Color.LightBlue;
                    }
                    if (isolatedStorageView != null)
                    {
                        isolatedStorageView.Visible = true;
                        isolatedStorageView.RefreshView();
                    }
                    break;
            }
        }
        private void ClickButton(IntPtr controlHandle, int controlNumber)
            {
                string className = windowApiService.QueryControlClassName(controlHandle);
                PerformClickByClassName(controlHandle, className);
        }
        private void PerformClickByClassName(IntPtr controlHandle, string className) => 
            windowApiService.ClickControl(controlHandle, className);
        private void AnalyzeToolbarButtons(IntPtr toolbarHandle, StringBuilder result, List<ControlInfo> controlsToAdd)
        {
            result.AppendLine("=== 工具列按鈕分析 ===");
            result.AppendLine($"工具列句柄: 0x{toolbarHandle.ToInt64():X}");
            result.AppendLine();
            
            try
            {
                int buttonCount = windowApiService.QueryToolbarButtonCount(toolbarHandle);
                
                if (buttonCount == 0)
                {
                    result.AppendLine("無法讀取工具列按鈕數量（工具列可能無效或已關閉）。");
                    result.AppendLine();
                    return;
                }
                
                result.AppendLine($"工具列包含 {buttonCount} 個按鈕");
                result.AppendLine();
                result.AppendLine("注意：");
                result.AppendLine("• 工具列按鈕文字無法直接讀取（可能導致目標程式崩潰）");
                result.AppendLine("• 0x3D0630 是「啟動」按鈕所在的工具列");
                result.AppendLine("• 「執行」按鈕可能在同一個工具列中，或在相鄰的工具列（0x50702）");
                result.AppendLine("• 需要通過工具列句柄和按鈕索引來點擊按鈕");
                result.AppendLine();
                result.AppendLine("建議：");
                result.AppendLine("• 如果「執行」在 0x3D0630 工具列中，可能是索引 1（「啟動」是索引 0）");
                result.AppendLine("• 如果「執行」在 0x50702 工具列中，可能是索引 0");
                result.AppendLine();
            }
            catch (Exception ex)
            {
                result.AppendLine($"讀取工具列資訊時發生錯誤：{ex.Message}");
                result.AppendLine("已跳過工具列按鈕分析，避免目標程式崩潰。");
                result.AppendLine();
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (clickModeService.IsActive)
                    {
                clickModeService.Disable();
            }
            if (clickWindowService.IsActive)
            {
                clickWindowService.Disable();
            }
            if (captureWindowService.IsActive)
                    {
                captureWindowService.Disable();
            }
            base.OnFormClosing(e);
        }
    }
}
