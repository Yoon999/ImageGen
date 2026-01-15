using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Models;
using ImageGen.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace ImageGen.Views.Controls;

public partial class NodeGraphControl : UserControl
{
    private bool _isDraggingNode;
    private Point _clickPosition;
    private GenerationNode? _draggedNode;

    public NodeGraphControl()
    {
        InitializeComponent();
        IsVisibleChanged += NodeGraphControl_IsVisibleChanged;
        DataContextChanged += NodeGraphControl_DataContextChanged;
    }

    private void NodeGraphControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is NodeGraphViewModel vm)
        {
            vm.RequestBringIntoView += Vm_RequestBringIntoView;
        }
        if (e.OldValue is NodeGraphViewModel oldVm)
        {
            oldVm.RequestBringIntoView -= Vm_RequestBringIntoView;
        }
    }

    private void Vm_RequestBringIntoView(object? sender, NodeType type)
    {
        if (DataContext is NodeGraphViewModel vm)
        {
            var targetNode = vm.Nodes.FirstOrDefault(n => n.Type == type);
            if (targetNode != null)
            {
                // Find the ScrollViewer
                var scrollViewer = FindVisualChild<ScrollViewer>(this);
                if (scrollViewer != null)
                {
                    // Center the node
                    double x = targetNode.UiX * vm.ZoomScale;
                    double y = targetNode.UiY * vm.ZoomScale;
                    
                    double viewportWidth = scrollViewer.ViewportWidth;
                    double viewportHeight = scrollViewer.ViewportHeight;
                    
                    scrollViewer.ScrollToHorizontalOffset(x - viewportWidth / 2 + (targetNode.Width * vm.ZoomScale / 2));
                    scrollViewer.ScrollToVerticalOffset(y - viewportHeight / 2 + (targetNode.Height * vm.ZoomScale / 2));
                }
            }
        }
    }
    
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                return t;
            }
            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private void NodeGraphControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            if (DataContext is NodeGraphViewModel vm)
            {
                vm.RefreshPresetsCommand.Execute(null);
            }
            // Focus to enable key bindings
            this.Focus();
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clear focus from any text box when clicking on the canvas background
        Keyboard.ClearFocus();
        this.Focus(); // Ensure UserControl has focus for key bindings

        // Cancel connection if clicking on empty canvas
        if (DataContext is NodeGraphViewModel vm && vm.IsConnecting)
        {
            vm.CancelConnectionCommand.Execute(null);
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GenerationNode node)
        {
            // Clear focus when clicking on a node (e.g. to drag), unless clicking a TextBox which handles the event itself.
            Keyboard.ClearFocus();
            this.Focus();

            // If we are in connecting mode, this click might be to complete the connection
            if (DataContext is NodeGraphViewModel vm && vm.IsConnecting)
            {
                vm.CompleteConnectionCommand.Execute(node);
                e.Handled = true;
                return;
            }

            _isDraggingNode = true;
            _draggedNode = node;
            _clickPosition = e.GetPosition(this);
            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingNode && sender is FrameworkElement element)
        {
            _isDraggingNode = false;
            _draggedNode = null;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var currentControlPosition = e.GetPosition(this);

        if (DataContext is NodeGraphViewModel vm)
        {
            // Update temporary connection line if connecting
            if (vm.IsConnecting)
            {
                // Use position relative to the canvas (logical coordinates) to account for zoom/pan
                if (sender is IInputElement canvas)
                {
                    var logicalPosition = e.GetPosition(canvas);
                    vm.UpdateTempConnection(logicalPosition.X, logicalPosition.Y);
                }
            }
            
            // Move node if dragging
            if (_isDraggingNode && _draggedNode != null)
            {
                var offset = currentControlPosition - _clickPosition;
                
                // Adjust movement by zoom scale to keep dragging consistent
                _draggedNode.UiX += offset.X / vm.ZoomScale;
                _draggedNode.UiY += offset.Y / vm.ZoomScale;
                
                _clickPosition = currentControlPosition;
            }
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Canvas release logic
    }

    private void Node_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GenerationNode node)
        {
            node.Width = e.NewSize.Width;
            node.Height = e.NewSize.Height;
        }
    }

    private void InputPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Keyboard.ClearFocus();

        // Ctrl + Click on Input Port to disconnect incoming connections
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (sender is FrameworkElement element && element.DataContext is GenerationNode node)
            {
                if (DataContext is NodeGraphViewModel vm)
                {
                    vm.DisconnectInputCommand.Execute(node);
                    e.Handled = true;
                }
            }
        }
    }

    private void OutputPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Keyboard.ClearFocus();

        // Ctrl + Click on Output Port to disconnect outgoing connection
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (sender is FrameworkElement element && element.DataContext is GenerationNode node)
            {
                if (DataContext is NodeGraphViewModel vm)
                {
                    vm.ClearConnectionsCommand.Execute(node);
                    e.Handled = true;
                }
            }
        }
    }

    private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Check for Ctrl + Shift + MouseWheel
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (DataContext is NodeGraphViewModel vm)
            {
                if (e.Delta > 0)
                {
                    vm.ZoomInCommand.Execute(null);
                }
                else
                {
                    vm.ZoomOutCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (DataContext is NodeGraphViewModel vm)
            {
                vm.GoToBeginNodeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
