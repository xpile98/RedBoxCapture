using System;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SnippingTool
{
    public partial class MainWindow : Window
    {
        // 핫키 등록을 위한 상수
        private const int HOTKEY_ID = 9000;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_D = 0x44;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded; // 윈도우 로드 후 핫키 등록
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 핫키 등록
            var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
            var windowHandle = windowInteropHelper.Handle;

            if (windowHandle != IntPtr.Zero)
            {
                RegisterHotKey(windowHandle, HOTKEY_ID, MOD_CONTROL, VK_D);

                // 윈도우 메시지 처리기 등록
                var source = System.Windows.Interop.HwndSource.FromHwnd(windowHandle);
                source.AddHook(HwndHook);
            }
            else
            {
                System.Windows.MessageBox.Show("핸들을 얻지 못했습니다.");
            }
        }

        // 윈도우 메시지 처리기
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                CaptureSelectedRegion();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private async void CaptureSelectedRegion()
        {
            SnippingWindow snippingWindow = new SnippingWindow();
            bool result = await snippingWindow.ShowDialog();
            if (result)
            {
                SaveAndCopyToClipboard(snippingWindow.CapturedBitmap);
            }
        }

        private void SaveAndCopyToClipboard(Bitmap bitmap)
        {
            if (bitmap == null)
                return;

            try
            {
                // 고유한 파일 이름을 생성하여 저장
                string filePath = $"capture_{Guid.NewGuid()}.png";

                // 비트맵을 파일로 저장
                bitmap.Save(filePath, ImageFormat.Png);

                // 파일을 클립보드에 복사
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    var image = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
                        System.Windows.Media.Imaging.BitmapCreateOptions.None,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    System.Windows.Clipboard.SetImage(image);
                }
            }
            catch (ExternalException ex)
            {
                // 오류 메시지 처리
            }
            finally
            {
                // 비트맵 객체를 해제하여 메모리 누수 방지
                bitmap.Dispose();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 핫키 해제
            var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
            var windowHandle = windowInteropHelper.Handle;
            UnregisterHotKey(windowHandle, HOTKEY_ID);

            base.OnClosed(e);
        }
    }
}
