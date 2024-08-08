using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Point = System.Windows.Point;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SnippingTool
{
    public partial class SnippingWindow : Window
    {
        private Point startPoint;
        private bool isDragging;
        private TaskCompletionSource<bool> captureCompletionSource;
        private Rectangle selectionRectangle;

        public Bitmap CapturedBitmap { get; private set; }

        public SnippingWindow()
        {
            InitializeComponent();
            this.Cursor = System.Windows.Input.Cursors.Cross;
            this.Loaded += SnippingWindow_Loaded;
        }

        private void SnippingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetWindowPositionForAllScreens();
        }

        private void SetWindowPositionForAllScreens()
        {
            // 현재 윈도우의 HWND를 가져옵니다.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // 모든 스크린의 전체 영역을 계산
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            foreach (Screen screen in Screen.AllScreens)
            {
                left = Math.Min(left, screen.Bounds.Left);
                top = Math.Min(top, screen.Bounds.Top);
                right = Math.Max(right, screen.Bounds.Right);
                bottom = Math.Max(bottom, screen.Bounds.Bottom);
            }

            // Win32 API를 사용하여 윈도우 위치와 크기를 설정
            SetWindowPos(hwnd, IntPtr.Zero, left, top, right - left, bottom - top,
                SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                startPoint = e.GetPosition(this);
                isDragging = true;
                SelectionRectangle.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPoint = e.GetPosition(this);

                double x = Math.Min(currentPoint.X, startPoint.X);
                double y = Math.Min(currentPoint.Y, startPoint.Y);
                double width = Math.Abs(currentPoint.X - startPoint.X);
                double height = Math.Abs(currentPoint.Y - startPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;

                selectionRectangle = new Rectangle((int)x, (int)y, (int)width, (int)height);
            }
        }



        private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                SelectionRectangle.Visibility = Visibility.Hidden;

                Point mousePosition = e.GetPosition(this);
                POINT screenPoint = new POINT
                {
                    X = (int)(this.Left + mousePosition.X),
                    Y = (int)(this.Top + mousePosition.Y)
                };

                await CaptureAndCloseAsync(screenPoint);
            }
        }

        private async Task CaptureAndCloseAsync(POINT screenPoint)
        {
            this.Hide();
            await Task.Delay(100);

            CaptureTopMostWindow(screenPoint);
            captureCompletionSource.SetResult(true);
            this.Close();
        }

        private void CaptureTopMostWindow(POINT screenPoint)
        {
            IntPtr hWnd = WindowFromPoint(screenPoint);

            if (hWnd != IntPtr.Zero)
            {
                RECT finalRect;

                RECT windowRect;
                GetWindowRect(hWnd, out windowRect);
                finalRect = windowRect;

                RECT clientRect;
                GetClientRect(hWnd, out clientRect);

                POINT topLeft = new POINT { X = clientRect.Left, Y = clientRect.Top };
                POINT bottomRight = new POINT { X = clientRect.Right, Y = clientRect.Bottom };
                /*
                RECT rcFrame;
                DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rcFrame, Marshal.SizeOf(typeof(RECT)));

                if (rcFrame.Top != 0 && rcFrame.Left != 0 && rcFrame.Bottom != 0 && rcFrame.Right != 0)
                {
                    rcFrame.Top += 8;
                    rcFrame.Left += 3;
                    rcFrame.Bottom -= 3;
                    rcFrame.Right -= 3;
                    finalRect = rcFrame;
                }
                */

                // Get monitor info
                IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(hMonitor, ref monitorInfo);

                // Adjust window rect if it's fullscreen
                if (IsFullscreen(windowRect, monitorInfo.rcMonitor))
                {
                    windowRect = monitorInfo.rcMonitor;
                }

                // int width = windowRect.Right - windowRect.Left;
                // int height = windowRect.Bottom - windowRect.Top;
                int width = finalRect.Right - finalRect.Left;
                int height = finalRect.Bottom - finalRect.Top;


                if (ClientToScreen(hWnd, ref topLeft) && ClientToScreen(hWnd, ref bottomRight))
                {
                    width = bottomRight.X - topLeft.X;
                    height = bottomRight.Y - topLeft.Y;
                }


                CapturedBitmap = new Bitmap(width, height);

                using (Graphics g = Graphics.FromImage(CapturedBitmap))
                {
                    //g.CopyFromScreen(windowRect.Left, windowRect.Top, 0, 0, new System.Drawing.Size(width, height));
                    g.CopyFromScreen(finalRect.Left, finalRect.Top, 0, 0, new System.Drawing.Size(width, height));
                    DrawSelectionRectangle(g, windowRect);
                }
            }
        }

        private bool IsFullscreen(RECT windowRect, RECT monitorRect)
        {
            return windowRect.Left <= monitorRect.Left &&
                   windowRect.Top <= monitorRect.Top &&
                   windowRect.Right >= monitorRect.Right &&
                   windowRect.Bottom >= monitorRect.Bottom;
        }

        private void DrawSelectionRectangle(Graphics g, RECT windowRect)
        {
            if (selectionRectangle.Width > 0 && selectionRectangle.Height > 0)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    Rectangle adjustedRect = new Rectangle(
                        selectionRectangle.X - windowRect.Left,
                        selectionRectangle.Y - windowRect.Top,
                        selectionRectangle.Width,
                        selectionRectangle.Height);

                    g.DrawRectangle(pen, adjustedRect);
                }
            }
        }

        public new Task<bool> ShowDialog()
        {
            captureCompletionSource = new TaskCompletionSource<bool>();
            base.ShowDialog();
            return captureCompletionSource.Task;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [Flags]
        private enum SetWindowPosFlags : uint
        {
            SWP_NOSIZE = 0x0001,
            SWP_NOMOVE = 0x0002,
            SWP_NOZORDER = 0x0004,
            SWP_NOREDRAW = 0x0008,
            SWP_NOACTIVATE = 0x0010,
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }


        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    }
}