using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPF_lich_su_kien_chuot_va_ban_phim.Model;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    /// <summary>
    /// Interaction logic for HOOK__UC_main.xaml
    /// </summary>
    public partial class HOOK__UC_main : UserControl
    {
        private control_server_class controlServer;
        private string Selected_file_replay = " log\\mouse_log0.csv log\\keyboard_log0.csv";
        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;
        private bool togger_record = false;
        private bool togger_replay = false;

        public HOOK__UC_main()
        {
            InitializeComponent();
            controlServer = new control_server_class();
            controlServer.RunServer();
            controlServer.ConnectToPipeServer();
            // Scale toàn bộ UI sau khi load
            this.Loaded += HOOK__UC_main_Loaded;
            LoadFilePairs(@"server\log");
        }

        private void LoadFilePairs(string folderPath)
        {
            // Lấy tất cả file mouse_log*.csv và keyboard_log*.csv
            var mouseFiles = Directory.GetFiles(folderPath, "mouse_log*.csv").OrderBy(f => f).ToList();
            var keyboardFiles = Directory.GetFiles(folderPath, "keyboard_log*.csv").OrderBy(f => f).ToList();

            int count = Math.Min(mouseFiles.Count, keyboardFiles.Count);
            var filePairs = new List<string>();

            for (int i = 0; i < count; i++)
            {
                // Ghép 2 file thành 1 string, xuống dòng giữa 2 tên
                string pair = $"log\\{System.IO.Path.GetFileName(mouseFiles[i])} log\\{System.IO.Path.GetFileName(keyboardFiles[i])}";
                filePairs.Add(pair);
            }

            FilePairListBox.ItemsSource = filePairs;
        }
        private void FilePairListBox_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FilePairListBox.SelectedItem != null)
            {
                Selected_file_replay = FilePairListBox.SelectedItem.ToString();
                MessageBox.Show(Selected_file_replay, "Selected File Pair");

                LoadFilePairs(@"server\log");
            }
        }


        private void HOOK__UC_main_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyScale(this);
        }

        private void ApplyScale(FrameworkElement parent)
        {
            double factor = ScaleFactor;

            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(parent).OfType<FrameworkElement>())
            {
                // Scale Width / Height nếu đã set
                if (!double.IsNaN(child.Width))
                    child.Width *= factor;
                if (!double.IsNaN(child.Height))
                    child.Height *= factor;

                // Scale Margin
                child.Margin = new Thickness(
                    child.Margin.Left * factor,
                    child.Margin.Top * factor,
                    child.Margin.Right * factor,
                    child.Margin.Bottom * factor
                );

                // Recursively scale children
                if (child is Panel || child is ContentControl || child is UserControl)
                {
                    ApplyScale(child);
                }
            }
        }

       

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (togger_record)
            {
                togger_record = false;
                controlServer.SendCommand("STOP");
                Record_on.Visibility = Visibility.Hidden;
                Record_off.Visibility = Visibility.Visible;
                Record.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            }
            else
            {
                togger_record = true;
                controlServer.SendCommand("START");
                Record_on.Visibility = Visibility.Visible;
                Record_off.Visibility = Visibility.Hidden;
                Record.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            }
            LoadFilePairs(@"server\log");
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {

            Replay_on.Visibility = Visibility.Visible;
            Replay_off.Visibility = Visibility.Hidden;
            Replay.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");

            controlServer.SendCommand($"REPLAY {Selected_file_replay} 2");
            await Task.Delay(200);

            Replay_on.Visibility = Visibility.Hidden;
            Replay_off.Visibility = Visibility.Visible;
            Replay.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");

            // Delay 200ms để tạo hiệu ứng nhấn
            

            // Phần "true" (thả)
            
        }

        
    }
}
