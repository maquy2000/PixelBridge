using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelBridge
{
    public class SystemConfiguration
    {
        /// <summary>
        /// IP của server
        /// </summary>
        public string IP { get; set; } = "127.0.0.1";
        /// <summary>
        /// Port number của server
        /// </summary>
        public int Port { get; set; } = 6677;
        /// <summary>
        /// Loại dữ liệu gửi đi: chuỗi trong TextBox hay chuỗi cố định
        /// </summary>
        public SendDataMode SendDataMode { get; set; } = SendDataMode.StringInTextBox;
        /// <summary>
        /// Chuỗi kết thúc chuỗi gửi đi
        /// </summary>
        public EndStringMode EndStringMode { get; set; } = EndStringMode.None;
        /// <summary>
        /// Chuỗi gửi đi nếu là cố định
        /// </summary>
        public string FixedString { get; set; } = "CHECK";
        /// <summary>
        /// Dùng nút RESET, để gửi chuỗi RESET
        /// </summary>
        public bool IsUseResetButton { get; set; } = false;
        public string ResetString { get; set; } = "RESET";
        /// <summary>
        /// Dùng nút SPACE, để gửi chuỗi SPACE
        /// </summary>
        public bool IsUseSpaceButton { get; set; } = false;
        public string SpaceString { get; set; } = "CHECK";
        /// <summary>
        /// Chế độ compare độ dài dữ liệu đầu vào
        /// </summary>
        public bool IsCompareLengthOfInputData { get; set; } = false;
        public int LengthOfInputCompare { get; set; } = 13;
        /// <summary>
        /// Chế đọ kiểm tra chuỗi con trong dữ liệu đầu vào
        /// </summary>
        public bool IsInputDataContain { get; set; } = false;
        public string InputDataContainString { get; set; } = "A2";


    }
}
