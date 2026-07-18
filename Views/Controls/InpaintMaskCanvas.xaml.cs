using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ImageGen.Services;
using InkCanvas = System.Windows.Controls.InkCanvas;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ImageGen.Views.Controls;

public partial class InpaintMaskCanvas : UserControl
{
    private const int MaxHistoryEntries = 30;
    private readonly List<StrokeCollection> _undoHistory = new();
    private readonly List<StrokeCollection> _redoHistory = new();
    private bool _isEraser;
    private bool _isPanning;
    private bool _isEditingMask;
    private bool _isRestoringHistory;
    private bool _overlayRefreshPending;
    private bool _hasFitted;
    private Point _lastPanPoint;
    private double _fitScale = 1;
    private double _zoomFactor = 1;
    private double _brushSize = 64;
    private bool _featherEnabled = true;
    private double _featherSize = 12;

    public InpaintMaskCanvas()
    {
        InitializeComponent();
        MaskInkCanvas.Strokes.StrokesChanged += Strokes_StrokesChanged;
        ApplyDrawingSettings();
    }

    public event EventHandler? MaskChanged;
    public event EventHandler? ViewChanged;
    public event EventHandler? HistoryChanged;

    public int ImagePixelWidth { get; private set; }
    public int ImagePixelHeight { get; private set; }
    public int RequestPixelWidth { get; private set; }
    public int RequestPixelHeight { get; private set; }
    public bool IsV4Mask { get; private set; }
    public bool HasMask => MaskInkCanvas.Strokes.Count > 0;
    public bool CanUndo => _undoHistory.Count > 0;
    public bool CanRedo => _redoHistory.Count > 0;
    public int ZoomPercent => (int)Math.Round(_zoomFactor * 100);

    public double BrushSize
    {
        get => _brushSize;
        set
        {
            _brushSize = Math.Clamp(value, 8, 512);
            ApplyDrawingSettings();
            UpdateCursorSize();
        }
    }

    public bool IsEraser => _isEraser;
    public bool FeatherEnabled => _featherEnabled;
    public double FeatherSize => _featherSize;

    public void Initialize(
        BitmapSource source,
        StrokeCollection? strokes = null,
        bool featherEnabled = true,
        double featherSize = 12,
        int? requestPixelWidth = null,
        int? requestPixelHeight = null,
        bool isV4Mask = true)
    {
        ImagePixelWidth = source.PixelWidth;
        ImagePixelHeight = source.PixelHeight;
        RequestPixelWidth = requestPixelWidth ?? ImagePixelWidth;
        RequestPixelHeight = requestPixelHeight ?? ImagePixelHeight;
        IsV4Mask = isV4Mask;
        SourceImage.Source = source;
        ImageSurface.Width = ImagePixelWidth;
        ImageSurface.Height = ImagePixelHeight;
        SurfaceHost.Width = ImagePixelWidth;
        SurfaceHost.Height = ImagePixelHeight;
        MaskInkCanvas.Width = ImagePixelWidth;
        MaskInkCanvas.Height = ImagePixelHeight;

        ReplaceStrokes(strokes?.Clone() ?? new StrokeCollection());
        SetFeather(featherEnabled, featherSize);
        _undoHistory.Clear();
        _redoHistory.Clear();
        BrushSize = Math.Clamp(Math.Min(ImagePixelWidth, ImagePixelHeight) * 0.04, 16, 128);
        _hasFitted = false;
        RefreshRenderedOverlay();
        Dispatcher.BeginInvoke(FitToViewport);
        RaiseStateChanged();
    }

    public StrokeCollection GetStrokes()
    {
        return MaskInkCanvas.Strokes.Clone();
    }

    public void SetTool(bool useEraser)
    {
        _isEraser = useEraser;
        ApplyDrawingSettings();
        UpdateCursorAppearance();
    }

    public void SetFeather(bool enabled, double size)
    {
        _featherEnabled = enabled;
        _featherSize = Math.Clamp(size, 1, 64);
        ScheduleRenderedOverlayRefresh();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        AddHistoryEntry(_redoHistory, MaskInkCanvas.Strokes.Clone());
        var previous = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        ReplaceStrokes(previous.Clone());
        RaiseStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        AddHistoryEntry(_undoHistory, MaskInkCanvas.Strokes.Clone());
        var next = _redoHistory[^1];
        _redoHistory.RemoveAt(_redoHistory.Count - 1);
        ReplaceStrokes(next.Clone());
        RaiseStateChanged();
    }

    public void ClearMask()
    {
        if (!HasMask) return;
        PushUndoState();
        MaskInkCanvas.Strokes.Clear();
        RaiseStateChanged();
    }

    public void FitToViewport()
    {
        if (ImagePixelWidth <= 0 || ImagePixelHeight <= 0 || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return;
        }

        _fitScale = Math.Min(
            Viewport.ActualWidth / ImagePixelWidth,
            Viewport.ActualHeight / ImagePixelHeight);
        _zoomFactor = 1;
        ApplyViewTransform(center: true);
        _hasFitted = true;
    }

    public void ZoomIn() => SetZoomFactor(_zoomFactor * 1.2, new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2));
    public void ZoomOut() => SetZoomFactor(_zoomFactor / 1.2, new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2));

    private void ApplyDrawingSettings()
    {
        var attributes = MaskInkCanvas.DefaultDrawingAttributes;
        attributes.Color = Color.FromRgb(255, 55, 55);
        attributes.Width = BrushSize;
        attributes.Height = BrushSize;
        attributes.StylusTip = StylusTip.Ellipse;
        attributes.IgnorePressure = true;
        attributes.FitToCurve = true;
        attributes.IsHighlighter = false;

        MaskInkCanvas.EraserShape = new EllipseStylusShape(BrushSize, BrushSize);
        if (!_isPanning)
        {
            MaskInkCanvas.EditingMode = _isEraser ? InkCanvasEditingMode.EraseByPoint : InkCanvasEditingMode.Ink;
        }
    }

    private void PushUndoState()
    {
        AddHistoryEntry(_undoHistory, MaskInkCanvas.Strokes.Clone());
        _redoHistory.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AddHistoryEntry(List<StrokeCollection> history, StrokeCollection entry)
    {
        history.Add(entry);
        if (history.Count > MaxHistoryEntries)
        {
            history.RemoveAt(0);
        }
    }

    private void ReplaceStrokes(StrokeCollection strokes)
    {
        _isRestoringHistory = true;
        MaskInkCanvas.Strokes.StrokesChanged -= Strokes_StrokesChanged;
        MaskInkCanvas.Strokes = strokes;
        MaskInkCanvas.Strokes.StrokesChanged += Strokes_StrokesChanged;
        _isRestoringHistory = false;
        ScheduleRenderedOverlayRefresh();
    }

    private void Strokes_StrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (!_isRestoringHistory)
        {
            ScheduleRenderedOverlayRefresh();
            MaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseStateChanged()
    {
        ScheduleRenderedOverlayRefresh();
        MaskChanged?.Invoke(this, EventArgs.Empty);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Viewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var shouldPan = e.ChangedButton == MouseButton.Middle
                        || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space));
        if (shouldPan)
        {
            _isPanning = true;
            _lastPanPoint = e.GetPosition(Viewport);
            MaskInkCanvas.EditingMode = InkCanvasEditingMode.None;
            Viewport.CaptureMouse();
            BrushCursor.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _isEditingMask = true;
            RenderedMaskOverlay.Visibility = Visibility.Collapsed;
            MaskInkCanvas.Opacity = 0.47;
            PushUndoState();
        }
    }

    private void Viewport_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Viewport.ReleaseMouseCapture();
            ApplyDrawingSettings();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _isEditingMask = false;
            ScheduleRenderedOverlayRefresh();
        }
    }

    private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(Viewport);
        if (_isPanning)
        {
            var delta = point - _lastPanPoint;
            SurfaceTranslate.X += delta.X;
            SurfaceTranslate.Y += delta.Y;
            _lastPanPoint = point;
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        Canvas.SetLeft(BrushCursor, point.X - BrushCursor.Width / 2);
        Canvas.SetTop(BrushCursor, point.Y - BrushCursor.Height / 2);
        BrushCursor.Visibility = Visibility.Visible;
    }

    private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        SetZoomFactor(_zoomFactor * (e.Delta > 0 ? 1.2 : 1 / 1.2), e.GetPosition(Viewport));
        e.Handled = true;
    }

    private void SetZoomFactor(double value, Point anchor)
    {
        var newFactor = Math.Clamp(value, 0.25, 8);
        var oldScale = SurfaceScale.ScaleX;
        var newScale = _fitScale * newFactor;
        if (oldScale <= 0 || Math.Abs(newScale - oldScale) < 0.0001) return;

        var sourceX = (anchor.X - SurfaceTranslate.X) / oldScale;
        var sourceY = (anchor.Y - SurfaceTranslate.Y) / oldScale;
        _zoomFactor = newFactor;
        SurfaceScale.ScaleX = newScale;
        SurfaceScale.ScaleY = newScale;
        SurfaceTranslate.X = anchor.X - sourceX * newScale;
        SurfaceTranslate.Y = anchor.Y - sourceY * newScale;
        UpdateCursorSize();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyViewTransform(bool center)
    {
        var scale = _fitScale * _zoomFactor;
        SurfaceScale.ScaleX = scale;
        SurfaceScale.ScaleY = scale;
        if (center)
        {
            SurfaceTranslate.X = (Viewport.ActualWidth - ImagePixelWidth * scale) / 2;
            SurfaceTranslate.Y = (Viewport.ActualHeight - ImagePixelHeight * scale) / 2;
        }
        UpdateCursorSize();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCursorSize()
    {
        var displaySize = Math.Max(4, BrushSize * SurfaceScale.ScaleX);
        BrushCursor.Width = displaySize;
        BrushCursor.Height = displaySize;
    }

    private void UpdateCursorAppearance()
    {
        BrushCursor.Stroke = _isEraser ? Brushes.White : new SolidColorBrush(Color.FromRgb(255, 82, 82));
    }

    private void Viewport_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            BrushCursor.Visibility = Visibility.Collapsed;
        }
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_hasFitted)
        {
            FitToViewport();
        }
    }

    private void ScheduleRenderedOverlayRefresh()
    {
        if (_isEditingMask || _overlayRefreshPending || ImagePixelWidth <= 0 || ImagePixelHeight <= 0)
        {
            return;
        }

        _overlayRefreshPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _overlayRefreshPending = false;
            RefreshRenderedOverlay();
        }));
    }

    private void RefreshRenderedOverlay()
    {
        if (ImagePixelWidth <= 0 || ImagePixelHeight <= 0) return;

        if (!HasMask)
        {
            RenderedMaskOverlay.Source = null;
        }
        else
        {
            var mask = InpaintMaskRenderer.Render(
                MaskInkCanvas.Strokes,
                ImagePixelWidth,
                ImagePixelHeight,
                RequestPixelWidth,
                RequestPixelHeight,
                IsV4Mask,
                FeatherEnabled,
                FeatherSize);
            RenderedMaskOverlay.Source = InpaintMaskRenderer.CreateColorOverlay(
                mask,
                Color.FromRgb(255, 55, 55),
                0.47);
        }

        RenderedMaskOverlay.Visibility = Visibility.Visible;
        MaskInkCanvas.Opacity = 0;
    }
}
