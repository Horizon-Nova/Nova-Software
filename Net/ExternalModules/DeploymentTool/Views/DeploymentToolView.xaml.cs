using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ExternalModules.DeploymentTool.Components;
using ExternalModules.DeploymentTool.Models.Deployment;
using ExternalModules.DeploymentTool.ViewModels;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.DeploymentTool.Views;

public partial class DeploymentToolView : UserControl
{
    private readonly DeploymentToolPageViewModel _viewModel = new();
    private int _nextCardIndex;

    private Point _canvasDragStartPoint;
    private double _canvasDragStartTranslateX;
    private double _canvasDragStartTranslateY;
    private bool _isCanvasPointerDown;
    private bool _isCanvasDragging;
    private const double CanvasDragThreshold = 6d;

    private UserControl? _draggingCard;
    private Point _cardDragStartPoint;
    private double _cardDragStartLeft;
    private double _cardDragStartTop;
    private bool _isCardDragging;
    private const double CardDragThreshold = 6d;

    private UserControl? _selectedCard;
    private const double SidePanelExpandedWidth = 1280d;
    private const double MinZoom = 0.5d;
    private const double MaxZoom = 2.5d;
    private const double ZoomStep = 0.1d;

    public DeploymentToolView()
    {
        InitializeComponent();
        DataContext = _viewModel;

        PendingChangesBanner.DetailsClicked += (_, _) => DetailsButton_OnClick(this, new RoutedEventArgs());
        PendingChangesBanner.DeployClicked += (_, _) => DeployButton_OnClick(this, new RoutedEventArgs());
        PendingChangesBanner.DiscardClicked += (_, _) => DiscardChangesButton_OnClick(this, new RoutedEventArgs());

        ServiceSidePanel.CloseClicked += (_, _) => CloseServiceSidePanel();
        Loaded += DeploymentToolView_OnLoaded;
    }

    internal void ApplyHostContext(IShellContext context)
    {
    }

    internal void ClearHostContext()
    {
    }

    private void DeploymentToolView_OnLoaded(object sender, RoutedEventArgs e)
    {
        ServiceSidePanelHost.Width = 0d;
    }

    private void RootGrid_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        Focus();
        AddServicePopup.IsOpen = false;
        AddServicePopup.IsOpen = true;
        e.Handled = true;
    }

    private void Root_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        Point viewportCenter = new(CanvasViewport.ActualWidth / 2d, CanvasViewport.ActualHeight / 2d);

        if (e.Key is Key.OemPlus or Key.Add)
        {
            ZoomBy(ZoomStep, viewportCenter);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            ZoomBy(-ZoomStep, viewportCenter);
            e.Handled = true;
        }
    }

    private void CanvasViewport_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        if (sender is not Grid canvasViewport || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isCanvasPointerDown = true;
        _isCanvasDragging = false;
        _canvasDragStartPoint = e.GetPosition(this);
        _canvasDragStartTranslateX = CanvasTranslateTransform.X;
        _canvasDragStartTranslateY = CanvasTranslateTransform.Y;
    }

    private void CanvasViewport_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCanvasPointerDown || sender is not Grid canvasViewport)
        {
            return;
        }

        Point currentPoint = e.GetPosition(this);
        Vector dragVector = currentPoint - _canvasDragStartPoint;

        if (!_isCanvasDragging)
        {
            bool isOverThreshold = dragVector.Length >= CanvasDragThreshold;

            if (!isOverThreshold)
            {
                return;
            }

            _isCanvasDragging = true;
            canvasViewport.Cursor = Cursors.SizeAll;
            canvasViewport.CaptureMouse();
        }

        CanvasTranslateTransform.X = _canvasDragStartTranslateX + dragVector.X;
        CanvasTranslateTransform.Y = _canvasDragStartTranslateY + dragVector.Y;
        e.Handled = true;
    }

    private void CanvasViewport_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Grid canvasViewport || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        ResetCanvasDragState(canvasViewport);
    }

    private void CanvasViewport_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        Point zoomPivot = e.GetPosition(CanvasViewport);
        ZoomBy(e.Delta > 0 ? ZoomStep : -ZoomStep, zoomPivot);
        e.Handled = true;
    }

    private void CanvasViewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (sender is not Grid canvasViewport)
        {
            return;
        }

        ResetCanvasDragState(canvasViewport);
    }

    private void ResetCanvasDragState(Grid canvasViewport)
    {
        _isCanvasPointerDown = false;
        _isCanvasDragging = false;
        canvasViewport.Cursor = Cursors.Arrow;
        canvasViewport.ReleaseMouseCapture();
    }

    private void Card_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UserControl cardView)
        {
            return;
        }

        _draggingCard = cardView;
        _isCardDragging = false;
        _cardDragStartPoint = e.GetPosition(CanvasPanel);
        _cardDragStartLeft = Canvas.GetLeft(cardView);
        _cardDragStartTop = Canvas.GetTop(cardView);
        cardView.CaptureMouse();
        e.Handled = true;
    }

    private void Card_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingCard is null || !_draggingCard.IsMouseCaptured)
        {
            return;
        }

        Point currentPoint = e.GetPosition(CanvasPanel);
        Vector dragVector = currentPoint - _cardDragStartPoint;

        if (!_isCardDragging)
        {
            bool isOverThreshold = dragVector.Length >= CardDragThreshold;

            if (!isOverThreshold)
            {
                return;
            }

            _isCardDragging = true;
        }

        Canvas.SetLeft(_draggingCard, _cardDragStartLeft + dragVector.X);
        Canvas.SetTop(_draggingCard, _cardDragStartTop + dragVector.Y);
        e.Handled = true;
    }

    private void Card_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingCard is null)
        {
            return;
        }

        UserControl releasedCard = _draggingCard;
        bool shouldOpenSidePanel = !_isCardDragging;

        releasedCard.ReleaseMouseCapture();
        _draggingCard = null;
        _isCardDragging = false;

        if (shouldOpenSidePanel)
        {
            OpenServiceSidePanel(releasedCard);
        }

        e.Handled = true;
    }

    private void Card_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _draggingCard = null;
        _isCardDragging = false;
    }

    private void AddServiceOptionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string optionKey)
        {
            return;
        }

        AddCanvasCard(_viewModel.BuildCanvasItem(optionKey));
        RefreshPendingChangesBanner();
        AddServicePopup.IsOpen = false;
        e.Handled = true;
    }

    private void DetailsButton_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void DeployButton_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void DiscardChangesButton_OnClick(object sender, RoutedEventArgs e)
    {
        CanvasPanel.Children.Clear();
        _nextCardIndex = 0;
        _selectedCard = null;
        CloseServiceSidePanel();
        RefreshPendingChangesBanner();
    }

    private void RefreshPendingChangesBanner()
    {
        string pendingChangesText = _viewModel.QueryPendingChangesText(CanvasPanel.Children.Count);
        PendingChangesBanner.TitleText = pendingChangesText;
        PendingChangesBanner.Visibility = string.IsNullOrWhiteSpace(pendingChangesText)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AddCanvasCard(DeploymentCanvasItem item)
    {
        UserControl cardView = item.IsPending
            ? new PendingCard
            {
                Title = item.Title,
                StatusText = item.Subtitle,
                BadgeText = item.BadgeText,
                Margin = new Thickness(0, 0, 14, 14)
            }
            : new DeployedCard
            {
                Title = item.Title,
                Subtitle = item.Subtitle,
                StatusText = item.StatusText,
                Margin = new Thickness(0, 0, 14, 14)
            };

        cardView.PreviewMouseLeftButtonDown += Card_OnPreviewMouseLeftButtonDown;
        cardView.PreviewMouseMove += Card_OnPreviewMouseMove;
        cardView.PreviewMouseLeftButtonUp += Card_OnPreviewMouseLeftButtonUp;
        cardView.LostMouseCapture += Card_OnLostMouseCapture;

        const double startLeft = 0d;
        const double startTop = 0d;
        const double columnWidth = 340d;
        const double rowHeight = 200d;
        int columnIndex = _nextCardIndex % 3;
        int rowIndex = _nextCardIndex / 3;

        Canvas.SetLeft(cardView, startLeft + (columnIndex * columnWidth));
        Canvas.SetTop(cardView, startTop + (rowIndex * rowHeight));
        CanvasPanel.Children.Add(cardView);
        _nextCardIndex++;
    }

    private void OpenServiceSidePanel(UserControl selectedCard)
    {
        _selectedCard = selectedCard;

        ServiceSidePanel.Title = QueryCardTitle(selectedCard);
        AnimateWidth(ServiceSidePanelHost, SidePanelExpandedWidth, () => FocusCard(selectedCard));
    }

    private void CloseServiceSidePanel()
    {
        AnimateWidth(ServiceSidePanelHost, 0d, null);
    }

    private string QueryCardTitle(UserControl selectedCard) =>
        selectedCard switch
        {
            PendingCard pendingCard => pendingCard.Title,
            DeployedCard deployedCard => deployedCard.Title,
            _ => "Service"
        };

    private void FocusCard(UserControl selectedCard)
    {
        if (CanvasViewport.ActualWidth <= 0d || CanvasViewport.ActualHeight <= 0d)
        {
            return;
        }

        double cardLeft = Canvas.GetLeft(selectedCard);
        double cardTop = Canvas.GetTop(selectedCard);
        double cardWidth = selectedCard.ActualWidth > 0d ? selectedCard.ActualWidth : selectedCard.Width;
        double cardHeight = selectedCard.ActualHeight > 0d ? selectedCard.ActualHeight : selectedCard.Height;

        double cardCenterX = cardLeft + (cardWidth / 2d);
        double cardCenterY = cardTop + (cardHeight / 2d);
        double viewportCenterX = CanvasViewport.ActualWidth / 2d;
        double viewportCenterY = CanvasViewport.ActualHeight / 2d;

        double zoom = CanvasScaleTransform.ScaleX;
        AnimateTranslate(CanvasTranslateTransform, viewportCenterX - (cardCenterX * zoom), viewportCenterY - (cardCenterY * zoom));
    }

    private void ZoomBy(double delta, Point pivot)
    {
        double currentZoom = CanvasScaleTransform.ScaleX;
        double targetZoom = Math.Clamp(currentZoom + delta, MinZoom, MaxZoom);

        if (Math.Abs(targetZoom - currentZoom) < 0.001d)
        {
            return;
        }

        double translateX = CanvasTranslateTransform.X;
        double translateY = CanvasTranslateTransform.Y;
        double contentX = (pivot.X - translateX) / currentZoom;
        double contentY = (pivot.Y - translateY) / currentZoom;

        CanvasScaleTransform.ScaleX = targetZoom;
        CanvasScaleTransform.ScaleY = targetZoom;
        CanvasTranslateTransform.X = pivot.X - (contentX * targetZoom);
        CanvasTranslateTransform.Y = pivot.Y - (contentY * targetZoom);
    }

    private static void AnimateWidth(FrameworkElement element, double toValue, Action? completedAction)
    {
        DoubleAnimation animation = new()
        {
            To = toValue,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        if (completedAction is not null)
        {
            animation.Completed += (_, _) => completedAction();
        }

        element.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }

    private static void AnimateTranslate(System.Windows.Media.TranslateTransform translateTransform, double toX, double toY)
    {
        Duration duration = TimeSpan.FromMilliseconds(280);
        CubicEase easing = new()
        {
            EasingMode = EasingMode.EaseOut
        };

        DoubleAnimation xAnimation = new()
        {
            To = toX,
            Duration = duration,
            EasingFunction = easing
        };

        DoubleAnimation yAnimation = new()
        {
            To = toY,
            Duration = duration,
            EasingFunction = easing
        };

        translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, xAnimation);
        translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, yAnimation);
    }
}
