using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using UserControl = System.Windows.Controls.UserControl;

namespace ImageGen.Views.Controls;

public partial class CharacterPositionGrid : UserControl
{
    public static readonly DependencyProperty XValueProperty =
        DependencyProperty.Register(
            nameof(XValue),
            typeof(double),
            typeof(CharacterPositionGrid),
            new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    public static readonly DependencyProperty YValueProperty =
        DependencyProperty.Register(
            nameof(YValue),
            typeof(double),
            typeof(CharacterPositionGrid),
            new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step),
            typeof(double),
            typeof(CharacterPositionGrid),
            new FrameworkPropertyMetadata(0.1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridDivisionsProperty =
        DependencyProperty.Register(
            nameof(GridDivisions),
            typeof(int),
            typeof(CharacterPositionGrid),
            new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isDragging;

    public CharacterPositionGrid()
    {
        InitializeComponent();
        SnapsToDevicePixels = true;
    }

    public double XValue
    {
        get => (double)GetValue(XValueProperty);
        set => SetValue(XValueProperty, value);
    }

    public double YValue
    {
        get => (double)GetValue(YValueProperty);
        set => SetValue(YValueProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int GridDivisions
    {
        get => (int)GetValue(GridDivisionsProperty);
        set => SetValue(GridDivisionsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = GetGridBounds();
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(16, 21, 31)),
            new Pen(new SolidColorBrush(Color.FromRgb(55, 65, 81)), 1),
            bounds,
            6,
            6);

        int divisions = Math.Max(1, GridDivisions);
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(55, 65, 81)), 1);
        for (int i = 0; i <= divisions; i++)
        {
            double pos = i * (bounds.Width / divisions);
            drawingContext.DrawLine(gridPen, new Point(bounds.Left + pos, bounds.Top), new Point(bounds.Left + pos, bounds.Bottom));
            drawingContext.DrawLine(gridPen, new Point(bounds.Left, bounds.Top + pos), new Point(bounds.Right, bounds.Top + pos));
        }

        double x = Clamp01(XValue);
        double y = Clamp01(YValue);
        var marker = new Point(bounds.Left + x * bounds.Width, bounds.Top + y * bounds.Height);
        var markerBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
        var markerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 136, 136)), 2);

        drawingContext.DrawEllipse(markerBrush, null, marker, 4, 4);
        drawingContext.DrawLine(markerPen, new Point(marker.X - 8, marker.Y), new Point(marker.X + 8, marker.Y));
        drawingContext.DrawLine(markerPen, new Point(marker.X, marker.Y - 8), new Point(marker.X, marker.Y + 8));
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _isDragging = true;
        CaptureMouse();
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            StopDragging();
            return;
        }

        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_isDragging) return;

        UpdateFromPoint(e.GetPosition(this));
        StopDragging();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _isDragging = false;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double width = double.IsInfinity(constraint.Width) ? 140 : constraint.Width;
        double size = Math.Max(120, Math.Min(width, 180));
        return new Size(size, size);
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CharacterPositionGrid grid)
        {
            grid.InvalidateVisual();
        }
    }

    private void UpdateFromPoint(Point point)
    {
        var bounds = GetGridBounds();
        double localX = Math.Max(0, Math.Min(point.X - bounds.Left, bounds.Width));
        double localY = Math.Max(0, Math.Min(point.Y - bounds.Top, bounds.Height));

        XValue = Snap(localX / bounds.Width);
        YValue = Snap(localY / bounds.Height);
        InvalidateVisual();
    }

    private Rect GetGridBounds()
    {
        double padding = 6;
        double size = Math.Max(10, Math.Min(ActualWidth, ActualHeight) - padding * 2);
        double x = (ActualWidth - size) / 2;
        double y = (ActualHeight - size) / 2;
        return new Rect(x, y, size, size);
    }

    private double Snap(double value)
    {
        double step = Step <= 0 ? 0.1 : Step;
        return Clamp01(Math.Round(value / step) * step);
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0, Math.Min(1, value));
    }

    private void StopDragging()
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
