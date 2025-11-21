using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PixelBridge.SendData
{
    public enum DataSendType
    {
        BarcodeReader,
        CustomData
    }
    public partial class MainWindow : Window
    {
        private TcpClient? client;
        private bool isRunning;
        private string server = "127.0.0.1";
        private int port = 6677;
        private string configPath = "data.json";
        private CancellationTokenSource? cancellationTokenSource;
        private bool isDragging = false;
        private bool lastConnectionState = false;

        #region Kiểm soát kích thước window
        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }
            return (IntPtr)0;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            int MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            /// <summary>x coordinate of point.</summary>
            public int x;
            /// <summary>y coordinate of point.</summary>
            public int y;
            /// <summary>Construct a point of coordinates (x,y).</summary>
            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
            public static readonly RECT Empty = new RECT();
            public int Width { get { return Math.Abs(right - left); } }
            public int Height { get { return bottom - top; } }
            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }
            public RECT(RECT rcSrc)
            {
                left = rcSrc.left;
                top = rcSrc.top;
                right = rcSrc.right;
                bottom = rcSrc.bottom;
            }
            public bool IsEmpty { get { return left >= right || top >= bottom; } }
            public override string ToString()
            {
                if (this == Empty) { return "RECT {Empty}"; }
                return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
            }
            public override bool Equals(object obj)
            {
                if (!(obj is Rect)) { return false; }
                return (this == (RECT)obj);
            }
            /// <summary>Return the HashCode for this struct (not garanteed to be unique)</summary>
            public override int GetHashCode() => left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
            /// <summary> Determine if 2 RECT are equal (deep compare)</summary>
            public static bool operator ==(RECT rect1, RECT rect2) { return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom); }
            /// <summary> Determine if 2 RECT are different(deep compare)</summary>
            public static bool operator !=(RECT rect1, RECT rect2) { return !(rect1 == rect2); }
        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) =>
            {
                IntPtr handle = (new WindowInteropHelper(this)).Handle;
                HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WindowProc));
            };
        }

        private void panelHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDragging = true; // Bắt đầu kéo khi click panelHeader
                try
                {
                    DragMove();
                }
                finally
                {
                    isDragging = false; // Kết thúc kéo
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadConfig();

            StartConnectionCheckThread(); // Bắt đầu kiểm tra kết nối liên tục
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Window_Closed(sender, e);
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
            client?.Close();
            Application.Current.Shutdown();
        }

        private async Task LoadConfig()
        {
            if (!File.Exists(configPath))
            {
                WriteDefaultConfig();
                tbxShowErr.Text = "Khởi tạo lỗi: Kết nối tới server 127.0.0.1 : 6677";
                return;
            }
            try
            {
                string[] lines = File.ReadAllLines(configPath);
                string serverLine = Array.Find(lines, l => l.StartsWith("server="));
                string portLine = Array.Find(lines, l => l.StartsWith("port="));

                if (string.IsNullOrWhiteSpace(serverLine) ||
                    string.IsNullOrWhiteSpace(portLine) ||
                    !int.TryParse(portLine.Split('=')[1], out int portOut))
                {
                    WriteDefaultConfig();
                    return;
                }

                server = serverLine.Split('=')[1].Trim();
                port = portOut;
                tbxShowErr.Text = $"Kết nối tới server {server} : {port}";
            }
            catch
            {
                WriteDefaultConfig();
                tbxShowErr.Text = "Khởi tạo lỗi: Kết nối tới server 127.0.0.1 : 6677";
            }
        }

        private void WriteDefaultConfig()
        {
            File.WriteAllLines(configPath, new[]
            {
                "server=127.0.0.1",
                "port=6677"
            });
            server = "127.0.0.1";
            port = 6677;
        }

        private void ConnectionStatusNotify(object? sender, bool isConnected)
        {
            if (isConnected == lastConnectionState)
                return;
            lastConnectionState = isConnected;

            Dispatcher.BeginInvoke(() =>
            {
                iconConnectProgram.Text = isConnected ? "✅" : "❎";
                iconConnectProgram.Foreground = isConnected ? Brushes.Green : Brushes.Red;
            });
        }

        private void StartConnectionCheckThread()
        {
            isRunning = true;
            cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => CheckConnectionStatus(cancellationTokenSource.Token));
        }

        private async Task CheckConnectionStatus(CancellationToken token)
        {
            while (isRunning)
            {
                if (isDragging)
                {
                    // Tạm dừng kiểm tra khi kéo/thay đổi kích thước cửa sổ
                    await Task.Delay(2000);
                    continue;
                }

                if (token.IsCancellationRequested)
                    return;

                try
                {
                    var tcp = client;
                    if (tcp == null || !IsSocketConnected(tcp.Client))
                    {
                        ConnectionStatusNotify(this, false);
                        await Reconnect();
                    }
                }
                catch
                {
                    isRunning = false;
                    ConnectionStatusNotify(this, false);
                }

                await Task.Delay(2000);
            }
        }

        private async Task Reconnect()
        {
            while (isRunning)
            {
                try
                {
                    client?.Close();
                    client = new TcpClient();

                    await client.ConnectAsync(server, port);

                    if (client.Connected)
                    {
                        ConnectionStatusNotify(this, true);
                        return;
                    }
                }
                catch
                {
                    ConnectionStatusNotify(this, false);
                    await Task.Delay(3000);
                }
            }
        }

        private bool IsSocketConnected(Socket socket)
        {
            if (socket == null)
                return false;
            try
            {
                return !(socket.Poll(2000, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private async void tbxBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                tbxShowErr.Text = "";
                string dataSend = tbxBarcode.Text;
                await SendStringToServer(dataSend);
                tbxBarcode.Clear();
                tbxBarcode.Focusable = true;
            }
        }

        private async Task SendStringToServer(string message)
        {
            if (client != null && client.Connected)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch
                {
                    tbxShowErr.Text = "Có lỗi khi gửi dữ liệu!";
                }
            }
            else
            {
                tbxShowErr.Text = "Chưa kết nối tới server!";
            }
        }

        private async void btnReset_Click(object sender, RoutedEventArgs e)
        {
            tbxShowErr.Text = "RESET";
            await SendStringToServer("RESET");
        }
    }
}
