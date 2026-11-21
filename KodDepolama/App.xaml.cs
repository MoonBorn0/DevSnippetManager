using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace KodDepolama
{
    public partial class App : System.Windows.Application
    {
        private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static readonly uint ShowExistingInstanceMessage = RegisterWindowMessage("DevVault_Show");
        public static bool IsSystemShuttingDown { get; private set; }

        private Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            var safeUser = (Environment.UserName ?? "default").Replace('\\', '_');
            var mutexName = $"Local\\DevVault-SingleInstance-{safeUser}";
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: mutexName, out var createdNew);

            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;

                if (ShowExistingInstanceMessage != 0)
                {
                    PostMessage(HWND_BROADCAST, ShowExistingInstanceMessage, IntPtr.Zero, IntPtr.Zero);
                }

                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            IsSystemShuttingDown = true;
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.PrepareForExit();
            }

            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignore if mutex was not owned by current thread
                }
                finally
                {
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }

            base.OnExit(e);
        }
    }
}
