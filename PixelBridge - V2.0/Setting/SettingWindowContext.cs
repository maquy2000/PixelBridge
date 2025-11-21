using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PixelBridge
{
    public class SettingWindowContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public bool IsSaved { get; set; } = false;
        public RelayCommand SaveCommand { get; set; }
        public Array SendDataModeList => Enum.GetValues(typeof(SendDataMode));
        /// <summary>
        /// Config được load từ main context
        /// Hiển thị lên trên màn hình setting
        /// </summary>
        private SystemConfiguration _config { get; set; } = new SystemConfiguration();
        public SystemConfiguration Config
        {
            get { return _config; }
            set { _config = value; OnPropertyChanged(nameof(Config)); }
        }
        /// <summary>
        /// Config khi được save lại và đẩy ra ngoài để lưu vào file
        /// </summary>
        public SystemConfiguration ConfigSave { get; set; } = new SystemConfiguration();
        /// <summary>
        /// 
        /// </summary>
        public SettingWindowContext() 
        { 
            SaveCommand = new RelayCommand(OnSaveCommand);
        }
        /// <summary>
        /// Load data từ main
        /// </summary>
        /// <param name="config"></param>
        public void LoadSetting(SystemConfiguration config)
        {
            Config = config;
        }
        /// <summary>
        /// Save setting và đẩy ra ngoài
        /// </summary>
        /// <param name="o"></param>
        private void OnSaveCommand(object o)
        {
            // Đẩy config hiện tại ra ngoài để lưu
            IsSaved = true;
            ConfigSave = Config;
        }
    }
}
