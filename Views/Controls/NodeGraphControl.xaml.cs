using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Models;
using ImageGen.ViewModels;
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
    }

    private void NodeGraphControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            if (DataContext is NodeGraphViewModel vm)
            {
                vm.RefreshPresetsCommand.Execute(null);
            }
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clear focus from any text box when clicking on the canvas background
        Keyboard.ClearFocus();

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
        var currentPosition = e.GetPosition(this);

        if (DataContext is NodeGraphViewModel vm)
        {
            // Update temporary connection line if connecting
            if (vm.IsConnecting)
            {
                vm.UpdateTempConnection(currentPosition.X, currentPosition.Y);
            }
            
            // Move node if dragging
            if (_isDraggingNode && _draggedNode != null)
            {
                var offset = currentPosition - _clickPosition;
                
                _draggedNode.UiX += offset.X;
                _draggedNode.UiY += offset.Y;
                
                _clickPosition = currentPosition;
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
}
