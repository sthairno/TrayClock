using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Text;

namespace TrayClock
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public bool NoMutex { get; set; } = false;
        public string MutexName { get; set; } = "TrayClock";

        private Mutex mutex = null;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            foreach (var arg in e.Args)
            {
                switch (arg)
                {
                    case "--noMutex":
                        NoMutex = true;
                        break;
                }
            }

            bool mutexCreatedNew = true;
            if (!NoMutex)
            {
                mutex = new Mutex(false, MutexName, out mutexCreatedNew);
            }
            if (mutexCreatedNew)
            {
                Debug.WriteLine("Create MainWindow");
                MainWindow wnd = new MainWindow();
                wnd.Show();
            }
            else
            {
                mutex = null;

                Process other = GetPreviousProcess();
                if (other != null)
                {
                    Debug.WriteLine($"SendMessage to Process(PID:{other.Id})");
                    IntPtr hWnd = GetWindowHandle(other.Id, "MainWindow");
                    Debug.WriteLine($"hWnd:{hWnd}");
                    Debug.Write("SendMessage:");
                    if (WinAPI.SendMessage(hWnd, WinAPI.WM_APP, IntPtr.Zero, IntPtr.Zero) > 0)
                    {
                        Debug.WriteLine("OK");
                    }
                    else
                    {
                        Debug.WriteLine("Fail");
                    }
                }
                Shutdown();
            }
        }

        public static Process GetPreviousProcess()
        {
            Process curProcess = Process.GetCurrentProcess();
            Process[] allProcesses = Process.GetProcessesByName(curProcess.ProcessName);

            foreach (Process checkProcess in allProcesses)
            {
                if (checkProcess.Id != curProcess.Id)
                {
                    return checkProcess;
                }
            }

            return null;
        }

        public static IntPtr GetWindowHandle(int pid, string title)
        {
            var result = IntPtr.Zero;

            WinAPI.EnumDesktopWindows(IntPtr.Zero, (IntPtr hWnd, int lParam) =>
            {
                int id;
                WinAPI.GetWindowThreadProcessId(hWnd, out id);

                if (pid == id)
                {
                    var clsName = new StringBuilder(256);
                    var hasClass = WinAPI.GetClassName(hWnd, clsName, 256);
                    if (hasClass)
                    {
                        var maxLength = (int)WinAPI.GetWindowTextLength(hWnd);
                        var builder = new StringBuilder(maxLength + 1);
                        WinAPI.GetWindowText(hWnd, builder, (uint)builder.Capacity);

                        var text = builder.ToString();
                        var className = clsName.ToString();

                        if (text == title && className.StartsWith("HwndWrapper"))
                        {
                            result = hWnd;
                            return false;
                        }
                    }
                }
                return true;
            }
            , 0);

            return result;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (mutex != null)
            {
                mutex.Close();
            }
        }
    }
}
