using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PixelBridge
{
    /// <summary>
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        public SettingWindowContext Context { get; set; }
        public SettingWindow()
        {
            InitializeComponent();

            Context = new SettingWindowContext();
            this.DataContext = Context;            
        }
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }
}
