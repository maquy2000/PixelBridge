using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public TCPClient(string ip, int port)
        {
            IPAddress = ip;
            Port = port;
        }
        /// <summary>
        /// Kết nối 1 lần
        /// not in used
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress, Port);

                ConnectionStateChanged?.Invoke(true);
                return true;
            }
            catch
            {
                ConnectionStateChanged?.Invoke(false);
                return false;
            }
        }
        /// <summary>
        /// Ngắt kết nôi
        /// </summary>
        public void Disconnect()
        {
            try
            {
                tcpClient?.Close();
            }
            catch { }
        }
        private async Task<bool> IsSocketConnectedAsync(Socket socket, int timeoutMs = 300)
        {
            try
            {
                if (socket == null || !socket.Connected)
                    return false;

                // Kiểm tra remote-closed (FIN)
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    return false;

                // Gửi 0 byte để test
                var empty = Array.Empty<byte>();
                var sendTask = socket.SendAsync(empty, SocketFlags.None);

                // Timeout
                var completed = await Task.WhenAny(sendTask, Task.Delay(timeoutMs));

                if (completed != sendTask)
                    return false;   // timeout => mất kết nối

                // Nếu SendAsync ném lỗi → mất kết nối
                await sendTask;

                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// AUTO-RECONNECT LOOP
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

                    // Phát event khi thay đổi trạng thái
                    if (isAlive != _lastState)
                    {
                        _lastState = isAlive;
                        ConnectionStateChanged?.Invoke(isAlive);
                    }

                    // Nếu socket chết → cố gắng reconnect
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
        /// Kết thúc loop
        /// </summary>
        public void StopMonitorConnection()
        {
            _isMonitoring = false;
        }
        /// <summary>
        /// Try reconnect nếu đứt kết nối
        /// </summary>
        /// <returns></returns>
        private async Task TryReconnect()
        {
            if (_isReconnecting) return;   // tránh chạy nhiều lần
            _isReconnecting = true;

            try
            {
                Disconnect();

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(IPAddress, Port);

                // TCPClient.ConnectAsync KHÔNG đảm bảo socket còn sống 100%
                // nhưng tạm coi là thành công
                _lastState = true;
                ConnectionStateChanged?.Invoke(true);
            }
            catch
            {
                // thất bại => để vòng monitor thử lại
                await Task.Delay(1000);
            }
            finally
            {
                _isReconnecting = false;
            }
        }
        public bool SendData(string msg)
        {
            if (_lastState)
            {
                try
                {
                    ASCIIEncoding encoding = new ASCIIEncoding();
                    Stream stream = tcpClient.GetStream();
                    byte[] dataSend = encoding.GetBytes(msg);
                    stream.Write(dataSend, 0, dataSend.Length);
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            return false;
        }
        public bool ReadData(out string data, int timeoutMs = 100)
        {
            data = string.Empty;
            var stream = tcpClient.GetStream();
            stream.ReadTimeout = timeoutMs;             // QUAN TRỌNG: tránh block vô hạn

            var buf = new byte[BUFFER_SIZE];
            try
            {
                // Check mất kết nối REAL-TIME
                if (!_lastState)
                {
                    tcpClient.Close();
                    return false;
                }

                // Có thể check nhanh trước:
                if (!stream.DataAvailable) return false;

                int n = stream.Read(buf, 0, buf.Length); // block tối đa = timeoutMs
                if (n == 0) throw new IOException("Remote closed");
                data = Encoding.ASCII.GetString(buf, 0, n);
                return true;
            }
            catch (IOException ex) when (ex.InnerException is SocketException se
                                         && se.SocketErrorCode == SocketError.TimedOut)
            {
                // Hết thời gian chờ => coi như “chưa có data”, cho vòng lặp kiểm tra Cancel/Stop
                return false;
            }
        }
    }
}
