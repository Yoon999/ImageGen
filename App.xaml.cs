using System.Configuration;
using System.Data;
using System.Windows;
using ImageGen.Helpers;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ImageGen;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI 스레드에서 발생한 처리되지 않은 예외 처리
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        
        // UI 스레드 외에서 발생한 처리되지 않은 예외 처리 (Task 등)
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError("Unhandled UI Exception", e.Exception);
        MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\nCheck logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // 프로그램 종료 방지 (필요에 따라 false로 설정)
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.LogError("Unhandled Domain Exception", ex);
            MessageBox.Show($"A critical error occurred: {ex.Message}\nCheck logs for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
