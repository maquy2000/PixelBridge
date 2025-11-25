using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PixelBridge
{
    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum SendDataMode
    {
        /// <summary>
        /// 
        /// </summary>
        [Description("String in Textbox")]
        StringInTextBox,
        /// <summary>
        /// 
        /// </summary>
        [Description("Fixed string")]
        FixedString
    }
    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum EndStringMode
    {
        /// <summary>
        /// 
        /// </summary>
        [Description("None")]
        None,
        /// <summary>
        /// 
        /// </summary>
        [Description("<CR>")]
        CR,
        /// <summary>
        /// 
        /// </summary>
        [Description("<LF>")]
        LF,
        /// <summary>
        /// 
        /// </summary>
        [Description("<CR><LF>")]
        CRLF
    }
    /// <summary>
    /// 
    /// </summary>
    class MainContext : INotifyPropertyChanged
    {
        /// <summary>
        /// Event thay đổi lên giao diện
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        /// <summary>
        /// 
        /// </summary>
        private TCPClient tcp;
        private string _stringError = "";
        private bool _isConnected = false;
        private string configPath = "data.json";
        private System.Timers.Timer? _checkTimer;
        public string version { get; set; } = "2.0.2";
        public RelayCommand ExitCommand { get; set; }
        public RelayCommand SettingCommand { get; set; }
        public RelayCommand ResetCommand { get; set; }
        public string ConnectionIcon => IsConnected ? "✅" : "❎";
        public Brush ConnectionColor => IsConnected ? Brushes.Green : Brushes.Red;
        /// <summary>
        /// 
        /// </summary>
        public SystemConfiguration Config { get; set; } = new SystemConfiguration();
        public string StringError
        {
            get => _stringError;
            set
            {
                if (_stringError != value)
                {
                    _stringError = value;
                    OnPropertyChanged(nameof(StringError));
                }
            }
        }
        private string _dataToSendInTextBox = "";
        public string DataToSendInTextBox
        {
            get { return _dataToSendInTextBox; }
            set { _dataToSendInTextBox = value; OnPropertyChanged(nameof(DataToSendInTextBox)); }
        }
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    if (_isConnected)
                    {
                        StringError = $"Đã kết nối đến server {Config.IP} : {Config.Port}";
                    }
                    else
                    {
                        StringError = $"Kết nối đến server {Config.IP} : {Config.Port} gặp lỗi";
                    }
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(ConnectionIcon));
                    OnPropertyChanged(nameof(ConnectionColor));
                    OnPropertyChanged();
                }
            }
        }
        //public RelayCommand EnterTextBoxCommand { get; set; }
        //public RelayCommand EnterMainWindowCommand { get; set; }
        //public RelayCommand SpaceTextBoxCommand { get; set; }
        public RelayCommand SpaceMainWindowCommand { get; set; }
        public RelayCommand GlobalEnterCommand { get; set; }

        public MainContext()
        {
            ExitCommand = new RelayCommand(OnExitCommand);
            SettingCommand = new RelayCommand(OnSettingCommand);
            ResetCommand = new RelayCommand(OnResetCommand);
            //EnterTextBoxCommand = new RelayCommand(OnEnterTextBoxCommand);
            //EnterMainWindowCommand = new RelayCommand(OnEnterMainWindowCommand);
            //SpaceTextBoxCommand = new RelayCommand(OnSpaceTextBoxCommand);
            SpaceMainWindowCommand = new RelayCommand(OnSpaceMainWindowCommand);
            GlobalEnterCommand = new RelayCommand(OnGlobalEnterCommand);

            LoadConfiguration();
            StringError = $"Chờ kết nối đến server {Config.IP} : {Config.Port}";

            InitializeTCPClient();
        }
        /// <summary>
        /// Khởi động tcp chính
        /// </summary>
        private void InitializeTCPClient()
        {
            tcp = new TCPClient(Config.IP, Config.Port);
            tcp.EndStringSending = ConvertEndString(Config.EndStringMode);
            tcp.ConnectionStateChanged += (connected) =>
            {
                // Nếu không thay đổi thì KHÔNG update UI
                if (IsConnected == connected) return;
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsConnected = connected;
                });
            };
            tcp.StartMonitorConnection();
        }
        /// <summary>
        /// Chuyển đổi End String
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        private string ConvertEndString(EndStringMode mode)
        {
            return mode switch
            {
                EndStringMode.None => "",
                EndStringMode.CR => "\r",
                EndStringMode.LF => "\n",
                EndStringMode.CRLF => "\r\n",
                _ => "",
            };
        }
        //private void OnEnterTextBoxCommand(object obj)
        //{
        //    //MessageBox.Show("ENTER trong TextBox");
        //}
        //private void OnEnterMainWindowCommand(object obj)
        //{
        //    //MessageBox.Show("ENTER trong app (Window focus)");
        //}
        //private void OnSpaceTextBoxCommand(object obj)
        //{
        //    MessageBox.Show("SPACE trong TextBox");
        //    if (!string.IsNullOrEmpty(Config.SpaceString) && Config.IsUseSpaceButton == true)
        //    {
        //        bool isSendOK =  tcp.SendData(Config.SpaceString);
        //        DataToSendInTextBox = "";
        //        if (isSendOK) StringError = "";
        //    }
        //}
        private void OnSpaceMainWindowCommand(object obj)
        {
            //MessageBox.Show("SPACE trong app (Window focus)");
            if (!string.IsNullOrEmpty(Config.SpaceString) && Config.IsUseSpaceButton == true)
            {
                bool isSendOK = tcp.SendData(Config.SpaceString);
                DataToSendInTextBox = "";
                if (isSendOK) StringError = "";
            }
        }
        private void OnGlobalEnterCommand(object obj)
        {
            //MessageBox.Show("ENTER App không focus");
            if(Config.SendDataMode == SendDataMode.StringInTextBox)
            {
                if (!string.IsNullOrEmpty(DataToSendInTextBox))
                {
                    bool isDataInputOK = true;
                    if(Config.IsInputDataContain == true)
                    {
                        if(!string.IsNullOrEmpty(Config.InputDataContainString))
                            if (!DataToSendInTextBox.Contains(Config.InputDataContainString)) isDataInputOK = false;
                    }
                    if (Config.IsCompareLengthOfInputData == true)
                    {
                        if(Config.LengthOfInputCompare > 0)
                            if (DataToSendInTextBox.Length != Config.LengthOfInputCompare) isDataInputOK = false;
                    }

                    if (isDataInputOK == true)
                    {
                        bool isSendOK = tcp.SendData(DataToSendInTextBox);
                        DataToSendInTextBox = "";
                        if(isSendOK) StringError = "";
                    }
                    else
                    {
                        DataToSendInTextBox = "";
                        StringError = "Input data is invalid";
                    }
                }
            }
            else
            {
                bool isSendOK = tcp.SendData(Config.FixedString);
                if (isSendOK) StringError = "";
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        public void OnExitCommand(object o)
        {
            App.Current.Shutdown();
        }
        /// <summary>
        /// Cập nhật setting
        /// </summary>
        /// <param name="o"></param>
        public void OnSettingCommand(object o)
        {
            tcp.StopMonitorConnection();
            IsConnected = false;

            var frm = new SettingWindow();
            frm.Context.LoadSetting(Config);
            frm.ShowDialog();
            if(frm.Context.IsSaved == true)
            {
                Config = frm.Context.ConfigSave;
                SaveConfiguration(Config);
            }
            StringError = $"Chờ kết nối đến server {Config.IP} : {Config.Port}";

            // Kết nối lại IP:Port mới
            InitializeTCPClient();
        }
        /// <summary>
        /// Bấm vào nút RESET
        /// </summary>
        /// <param name="o"></param>
        public void OnResetCommand(object o)
        {
            if (!string.IsNullOrEmpty(Config.ResetString) && Config.IsUseResetButton == true)
            {
                bool isSendOK = tcp.SendData(Config.ResetString);
                DataToSendInTextBox = "";
                if (isSendOK) StringError = "";
            }
        }
        /// <summary>
        /// Load file config
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    // Deserialize từ JSON
                    var cfg = JsonSerializer.Deserialize<SystemConfiguration>(File.ReadAllText(configPath));
                    if (cfg != null)
                    {
                        Config = cfg;
                    }
                }
                else
                {
                    // Tạo file config mặc định nếu chưa có
                    SaveConfiguration(Config);
                }
            }
            catch (Exception ex)
            {
                StringError = "Load data lỗi, trở về data mặc định!";
            }
        }
        /// <summary>
        /// Lưu lại cấu hình vào file
        /// </summary>
        private void SaveConfiguration(SystemConfiguration config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                StringError = "Lỗi khi lưu file config!";
            }
        }
    }
}
