using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using XO_Launcher.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XO_Launcher
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>当前主窗口（供文件/文件夹选择器等场景获取窗口句柄）。</summary>
        public static Window? MainWindow { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            LauncherLogService.Info("App", "启动器进程已启动");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainWindow = _window;
            try { ModLibraryService.EnsureLayout(); }
            catch (Exception) { }
            _ = ModNameTranslationService.EnsureLoadedAsync();
            _window.Activate();
        }

        private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LauncherLogService.Error("App", "未处理的界面异常", e.Exception);
            e.Handled = true;
        }

        private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            LauncherLogService.Error("App", "未处理的进程异常", e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LauncherLogService.Error("App", "未观察的后台任务异常", e.Exception);
            e.SetObserved();
        }
    }
}
