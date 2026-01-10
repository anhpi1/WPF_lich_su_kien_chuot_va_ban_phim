using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using System.Windows.Threading;
using WPF_lich_su_kien_chuot_va_ban_phim.Model;
using static WPF_lich_su_kien_chuot_va_ban_phim.View.HOOK_UC_filter;


namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    /// <summary>
    /// Interaction logic for HOOK_UC_filterrealtime.xaml
    /// </summary>
    
    public partial class HOOK_UC_filterrealtime : UserControl
    {
        private control_server_class controlServer;
        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;
        DispatcherTimer _logTimer;
        public HOOK_UC_filterrealtime(control_server_class temp)
        {
            InitializeComponent();

            ActionList = new ObservableCollection<ActionItem>(); // 🔒 luôn tồn tại
            DataContext = this;

            controlServer = temp;

            _logTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _logTimer.Tick += LogTimer_Tick;

            Loaded += HOOK_UC_filter_Loaded;



            // Scale toàn bộ UI sau khi load
            this.Loaded += HOOK_UC_filter_Loaded;
        }

        private void HOOK_UC_filter_Loaded(object sender, RoutedEventArgs e)
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

        public class ActionItem
        {
            public string Icon { get; set; }
            public string Title { get; set; }
            public string Info { get; set; }
        }

        public ObservableCollection<ActionItem> ActionList { get; set; }
      


        public void AddActionItem(string icon, string title, string info)
        {
            ActionList.Insert(0, new ActionItem
            {
                Icon = icon,
                Title = title,
                Info = info
            });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            controlServer.SendCommand("START REALTIME REALTIME");
            await Task.Delay(500);
            if (!_logTimer.IsEnabled)
                _logTimer.Start();
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            LoadLogFile("server\\log_real_time\\log_real_time.csv");
        }

        private void LogTimer_Tick(object sender, EventArgs e)
        {
            LoadLogFile("server\\log_real_time\\log_real_time.csv");
        }


        string GetIconByEventName(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return "❓";

            eventName = eventName.ToUpperInvariant();

            // ERROR – ưu tiên cao nhất
            if (eventName.Contains("ERROR"))
                return "❗";

            // DRAG + DROP
            if (eventName.Contains("DRAG_DROP"))
                return "✋➜";

            // SCROLL / WHEEL
            if (eventName.Contains("SCROLL") || eventName.Contains("WHEEL"))
                return "🖱⬆⬇";

            // MOVE
            if (eventName.Contains("MOVE"))
                return "↔";

            // HOLD
            if (eventName.Contains("HOLD"))
                return "⏸";

            // COMBO (ưu tiên hơn KEY)
            if (eventName.Contains("COMBO"))
                return "⚡";

            // KEY
            if (eventName.Contains("KEY"))
                return "⌨";

            // CLICK / MOUSE
            if (eventName.Contains("CLICK") || eventName.Contains("MOUSE"))
                return "🖱";

            return "❓";
        }


        public void LoadLogFile(string filePath)
        {
            if (ActionList == null)
                return;
            ActionList.Clear();   // chỉ ẩn/hiện → reload UI

            if (!File.Exists(filePath))
                return;

            using var sr = new StreamReader(filePath);
            string line;

            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int comma = line.IndexOf(',');
                if (comma <= 0)
                    continue;

                string title = line.Substring(0, comma).Trim();
                string info = line.Substring(comma + 1).Trim();

                // 👉 FILTER Ở ĐÂY
                if (!IsEventAllowed(title))
                    continue;

                string icon = GetIconByEventName(title);

                AddActionItem(icon, title, info);
            }
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            controlServer.SendCommand("STOP");
            if (_logTimer.IsEnabled)
                _logTimer.Stop();
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            string path = "server\\log_real_time\\log_real_time.csv";

            controlServer.SendCommand("STOP");
            if (_logTimer.IsEnabled)
                _logTimer.Stop();

            await Task.Delay(500); // Đợi nửa giây để chắc chắn file đã đóng

            using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.SetLength(0);
            }

            ActionList.Clear();
        }

        bool IsEventAllowed(string eventName)
        {
            eventName = eventName.ToUpperInvariant();

            // ERROR
            if (eventName.Contains("ERROR"))
                return cbError.IsChecked == true;

            // KEY
            if (eventName.Contains("KEY_SEQUENCE"))
                return cbKeySequence.IsChecked == true;

            if (eventName.Contains("KEY_COMBO"))
                return cbKeyCombo.IsChecked == true;

            if (eventName.Contains("KEY_HOLD"))
                return cbKeyHold.IsChecked == true;

            if (eventName.Contains("KEY_PRESS"))
                return cbKeyPress.IsChecked == true;

            // MOUSE
            if (eventName.Contains("DRAG"))
                return cbMouseDrag.IsChecked == true;

            if (eventName.Contains("HOLD"))
                return cbMouseHold.IsChecked == true;

            if (eventName.Contains("MOVE"))
                return cbMouseMove.IsChecked == true;

            if (eventName.Contains("WHEEL"))
                return cbMouseWheel.IsChecked == true;

            if (eventName.Contains("CLICK"))
                return cbMouseClick.IsChecked == true;

            return true; // fallback
        }

    }
}
