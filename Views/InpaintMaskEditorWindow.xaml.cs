using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageGen.Models;
using ImageGen.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ImageGen.Views;

public partial class InpaintMaskEditorWindow : Window
{
    private readonly string _sourceImagePath;
    private readonly ImageEncodingService _imageEncodingService;

    public InpaintMaskEditorWindow(
        string sourceImagePath,
        BitmapSource sourceImage,
        InpaintMaskDocument? existingDocument,
        ImageEncodingService imageEncodingService)
    {
        InitializeComponent();
        _sourceImagePath = sourceImagePath;
        _imageEncodingService = imageEncodingService;

        var strokes = existingDocument?.SourceImagePath == sourceImagePath
            ? existingDocument.Strokes
            : null;
        var featherEnabled = existingDocument?.SourceImagePath == sourceImagePath
            ? existingDocument.FeatherEnabled
            : true;
        var featherSize = existingDocument?.SourceImagePath == sourceImagePath
            ? existingDocument.FeatherSize
            : 12;
        MaskCanvas.Initialize(sourceImage, strokes, featherEnabled, featherSize);
        MaskCanvas.MaskChanged += MaskCanvas_StateChanged;
        MaskCanvas.ViewChanged += MaskCanvas_StateChanged;
        MaskCanvas.HistoryChanged += MaskCanvas_StateChanged;
        BrushSizeSlider.Value = MaskCanvas.BrushSize;
        FeatherCheckBox.IsChecked = MaskCanvas.FeatherEnabled;
        FeatherSizeSlider.Value = MaskCanvas.FeatherSize;
        SelectTool(useEraser: false);
        UpdateStatus();
    }

    public InpaintMaskDocument? ResultDocument { get; private set; }

    private void BrushButton_Click(object sender, RoutedEventArgs e) => SelectTool(useEraser: false);
    private void EraserButton_Click(object sender, RoutedEventArgs e) => SelectTool(useEraser: true);
    private void UndoButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.Undo();
    private void RedoButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.Redo();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.ClearMask();
    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.ZoomOut();
    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.ZoomIn();
    private void FitButton_Click(object sender, RoutedEventArgs e) => MaskCanvas.FitToViewport();

    private void SelectTool(bool useEraser)
    {
        MaskCanvas.SetTool(useEraser);
        BrushButton.Opacity = useEraser ? 0.65 : 1;
        EraserButton.Opacity = useEraser ? 1 : 0.65;
    }

    private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        MaskCanvas.BrushSize = e.NewValue;
        UpdateStatus();
    }

    private void FeatherControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        MaskCanvas.SetFeather(FeatherCheckBox.IsChecked == true, FeatherSizeSlider.Value);
        UpdateStatus();
    }

    private void MaskCanvas_StateChanged(object? sender, EventArgs e)
    {
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        BrushSizeText.Text = $"{MaskCanvas.BrushSize:N0} px";
        FeatherSizeText.Text = $"{MaskCanvas.FeatherSize:N0} px";
        ZoomText.Text = $"Zoom {MaskCanvas.ZoomPercent}%";
        ResolutionText.Text = $"{MaskCanvas.ImagePixelWidth} x {MaskCanvas.ImagePixelHeight}";
        MaskStatusText.Text = MaskCanvas.HasMask ? "Mask ready" : "Mask not created";
        UndoButton.IsEnabled = MaskCanvas.CanUndo;
        RedoButton.IsEnabled = MaskCanvas.CanRedo;
        ClearButton.IsEnabled = MaskCanvas.HasMask;
        ApplyButton.IsEnabled = MaskCanvas.HasMask;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MaskCanvas.HasMask) return;

        var document = new InpaintMaskDocument(
            _sourceImagePath,
            MaskCanvas.ImagePixelWidth,
            MaskCanvas.ImagePixelHeight,
            MaskCanvas.GetStrokes(),
            MaskCanvas.FeatherEnabled,
            MaskCanvas.FeatherSize);
        document.Preview = _imageEncodingService.CreateInpaintMaskPreview(document);
        ResultDocument = document;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                MaskCanvas.Undo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y)
            {
                MaskCanvas.Redo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                MaskCanvas.FitToViewport();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.B:
                SelectTool(useEraser: false);
                e.Handled = true;
                break;
            case Key.E:
                SelectTool(useEraser: true);
                e.Handled = true;
                break;
            case Key.OemOpenBrackets:
                BrushSizeSlider.Value = Math.Max(BrushSizeSlider.Minimum, BrushSizeSlider.Value - 8);
                e.Handled = true;
                break;
            case Key.OemCloseBrackets:
                BrushSizeSlider.Value = Math.Min(BrushSizeSlider.Maximum, BrushSizeSlider.Value + 8);
                e.Handled = true;
                break;
        }
    }
}
