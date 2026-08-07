using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ImageGen.Helpers;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using TextBox = System.Windows.Controls.TextBox;

namespace ImageGen.Views.Controls;

public sealed class PromptWeightHighlightDecorator : Decorator
{
    private static readonly Color PositiveColor = Color.FromRgb(0x40, 0xC0, 0x00);
    private static readonly Color NegativeColor = Color.FromRgb(0xC0, 0x40, 0x00);

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background),
        typeof(Brush),
        typeof(PromptWeightHighlightDecorator),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    private TextBox? _textBox;
    private IReadOnlyList<PromptWeightSpan> _spans = Array.Empty<PromptWeightSpan>();
    private DispatcherOperation? _pendingRender;

    public PromptWeightHighlightDecorator()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ClipToBounds = true;
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

        if (_textBox == null || _spans.Count == 0) return;

        double positiveMax = _spans.Where(span => span.Weight > 0).Select(span => span.Weight).DefaultIfEmpty().Max();
        double negativeMaxMagnitude = _spans.Where(span => span.Weight < 0).Select(span => Math.Abs(span.Weight)).DefaultIfEmpty().Max();
        Point textBoxOffset = _textBox.TranslatePoint(new Point(), this);

        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        foreach (PromptWeightSpan span in _spans)
        {
            double intensity = span.Weight switch
            {
                > 0 when positiveMax > 0 => span.Weight / positiveMax,
                < 0 when negativeMaxMagnitude > 0 => Math.Abs(span.Weight) / negativeMaxMagnitude,
                _ => 0
            };
            if (intensity <= 0) continue;

            Color target = span.Weight > 0 ? PositiveColor : NegativeColor;
            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(byte.MaxValue * Math.Clamp(intensity, 0, 1)),
                target.R,
                target.G,
                target.B));
            brush.Freeze();

            foreach (Rect rect in GetHighlightRects(span))
            {
                Rect translated = rect;
                translated.Offset(textBoxOffset.X, textBoxOffset.Y);
                drawingContext.DrawRectangle(brush, null, translated);
            }
        }
        drawingContext.Pop();
    }

    private IEnumerable<Rect> GetHighlightRects(PromptWeightSpan span)
    {
        if (_textBox == null || span.Start < 0 || span.Start >= _textBox.Text.Length) yield break;

        int end = Math.Min(span.Start + span.Length, _textBox.Text.Length);
        Rect? currentLine = null;

        for (int index = span.Start; index < end; index++)
        {
            if (_textBox.Text[index] is '\r' or '\n')
            {
                if (currentLine is Rect lineBeforeBreak) yield return lineBeforeBreak;
                currentLine = null;
                continue;
            }

            Rect leading = _textBox.GetRectFromCharacterIndex(index, false);
            Rect trailing = _textBox.GetRectFromCharacterIndex(index, true);
            if (leading.IsEmpty || trailing.IsEmpty) continue;

            double left = Math.Min(leading.Left, trailing.Left);
            double right = Math.Max(leading.Right, trailing.Right);
            double top = Math.Min(leading.Top, trailing.Top);
            double bottom = Math.Max(leading.Bottom, trailing.Bottom);
            if (right <= left || bottom <= top) continue;

            var characterRect = new Rect(left, top, right - left, bottom - top);
            if (currentLine is Rect line
                && Math.Abs(line.Top - characterRect.Top) < 0.5
                && characterRect.Left <= line.Right + 1)
            {
                currentLine = Rect.Union(line, characterRect);
                continue;
            }

            if (currentLine is Rect completedLine) yield return completedLine;
            currentLine = characterRect;
        }

        if (currentLine is Rect finalLine) yield return finalLine;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_textBox, Child)) return;
        DetachTextBox();
        _textBox = Child as TextBox;
        if (_textBox == null) return;

        _textBox.TextChanged += OnTextChanged;
        _textBox.SizeChanged += OnTextBoxSizeChanged;
        _textBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged), true);
        RefreshSpans();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachTextBox();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshSpans();
    }

    private void OnTextBoxSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleRender();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        ScheduleRender();
    }

    private void RefreshSpans()
    {
        _spans = PromptWeightParser.Parse(_textBox?.Text);
        ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (_pendingRender?.Status == DispatcherOperationStatus.Pending) return;
        _pendingRender = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(InvalidateVisual));
    }

    private void DetachTextBox()
    {
        if (_textBox == null) return;
        _textBox.TextChanged -= OnTextChanged;
        _textBox.SizeChanged -= OnTextBoxSizeChanged;
        _textBox.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        _textBox = null;
        _spans = Array.Empty<PromptWeightSpan>();
    }
}
