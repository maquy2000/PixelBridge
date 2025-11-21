using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;                // để dùng Application.Current.Dispatcher

namespace PixelBridge
{
    public class TCPClient
    {
        private const int BUFFER_SIZE = 1024;

        public string IPAddress { get; set; }
        public int Port { get; set; }

        private TcpClient? tcpClient;

        private bool _isMonitoring = false;
        private bool _lastState = false;
        private bool _isReconnecting = false;

        public event Action<bool>? ConnectionStateChanged;

        public TCPClient(string ip, int port)
        {
            IPAddress = ip;
            Port = port;
        }
        /// <summary>
        /// Fire event lên UI thread nhưng dùng BeginInvoke (non-blocking)
        /// </summary>
        /// <param name="state"></param>
        private void RaiseConnectionState(bool state)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ConnectionStateChanged?.Invoke(state);
            }));
        }
        /// <summary>
        /// Kết nối 1 lần
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress, Port);
                RaiseConnectionState(true);
                return true;
            }
            catch
            {
                RaiseConnectionState(false);
                return false;
            }
        }
        /// <summary>
        /// Ngắt kết nối
        /// </summary>
        public void Disconnect()
        {
            try { tcpClient?.Close(); }
            catch { }
        }
        /// <summary>
        /// Kiểm tra kết nối socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        private async Task<bool> IsSocketConnectedAsync(Socket socket, int timeoutMs = 300)
        {
            try
            {
                if (socket == null || !socket.Connected)
                    return false;

                // remote closed
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    return false;

                // gửi 0 byte
                var empty = Array.Empty<byte>();
                var sendTask = socket.SendAsync(empty, SocketFlags.None);

                var completed = await Task.WhenAny(sendTask, Task.Delay(timeoutMs));
                if (completed != sendTask)
                    return false;

                await sendTask;
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Bắt đầu giám sát kết nối
        /// </summary>
        /// <param name="intervalMs"></param>
        public void StartMonitorConnection(int intervalMs = 1000)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;

            Task.Run(async () =>
            {
                while (_isMonitoring)
                {
                    bool isAlive = false;

                    if (tcpClient != null && tcpClient.Client != null)
                    {
                        isAlive = await IsSocketConnectedAsync(tcpClient.Client);
                    }

                    if (isAlive != _lastState)
                    {
                        _lastState = isAlive;
                        RaiseConnectionState(isAlive);
                    }

                    if (!isAlive)
                    {
                        await Task.Delay(2000);
                        await TryReconnect();
                    }

                    await Task.Delay(intervalMs);
                }
            });
        }
        /// <summary>
        /// Kết thúc việc giám sát kết nối
        /// </summary>
        public void StopMonitorConnection()
        {
            _isMonitoring = false;
        }
        /// <summary>
        /// Reconnect đến TCP server
        /// </summary>
        /// <returns></returns>
        private async Task TryReconnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                Disconnect();
                tcpClient = new TcpClient();

                await tcpClient.ConnectAsync(IPAddress, Port);

                _lastState = true;
                RaiseConnectionState(true);
            }
            catch
            {
                // thất bại, để vòng loop thử lại
                await Task.Delay(1000);
            }
            finally
            {
                _isReconnecting = false;
            }
        }
        /// <summary>
        /// Gửi data đến TCP server
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool SendData(string msg)
        {
            if (_lastState)
            {
                try
                {
                    Stream stream = tcpClient.GetStream();
                    byte[] dataSend = Encoding.ASCII.GetBytes(msg);
                    stream.Write(dataSend, 0, dataSend.Length);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
        /// <summary>
        /// Đọc data từ TCP server
        /// </summary>
        /// <param name="data"></param>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        public bool ReadData(out string data, int timeoutMs = 100)
        {
            data = string.Empty;

            if (tcpClient == null) return false;

            var stream = tcpClient.GetStream();
            stream.ReadTimeout = timeoutMs;

            var buf = new byte[BUFFER_SIZE];

            try
            {
                if (!_lastState)
                {
                    tcpClient.Close();
                    return false;
                }

                if (!stream.DataAvailable) return false;

                int n = stream.Read(buf, 0, buf.Length);
                if (n == 0) throw new IOException("Remote closed");

                data = Encoding.ASCII.GetString(buf, 0, n);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
