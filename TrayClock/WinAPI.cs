using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TrayClock
{
    static class WinAPI
    {
        public const int WM_APP = 0x8000;
        public const int WM_CLOSE = 0x0010;
        public const int WM_COPYDATA = 0x004A;
        public const int WM_SHOWWINDOW = 0x0018;
        public const int GWL_EXSTYLE = (-20);
        public const uint WS_EX_APPWINDOW = 0x40000;

        public const int SW_PARENTOPENING = 3;

        private const CharSet charSet = CharSet.Unicode;

        [StructLayout(LayoutKind.Sequential, CharSet = charSet)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpData;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = charSet)]
        public static extern int SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = charSet)]
        public static extern int SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public extern static bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lparam);

        [DllImport("user32.dll", CharSet = charSet)]
        public static extern bool GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc ewp, int lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = charSet)]
        public static extern uint GetWindowText(IntPtr hWnd, StringBuilder lpString, uint nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    }
}
