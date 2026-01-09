using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using WPF_lich_su_kien_chuot_va_ban_phim.Model;

namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    /// <summary>
    /// Interaction logic for HOOK_UC_filter.xaml
    /// </summary>
    public partial class HOOK_UC_filter : UserControl
    {
        private control_server_class controlServer;
        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;
        private ICollectionView ActionListView;
        public HOOK_UC_filter(control_server_class tempSever)
        {
            InitializeComponent();

            ActionList = new ObservableCollection<ActionItem>();
            DataContext = this;

            // Tạo CollectionView từ ActionList
            ActionListView = CollectionViewSource.GetDefaultView(ActionList);
            ActionListView.Filter = ActionListFilter;

            controlServer = tempSever;

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
            ActionList.Add(new ActionItem
            {
                Icon = icon,
                Title = title,
                Info = info
            });
        }

        private string GetIconFromEventName(string eventName)
        {
            string name = eventName.ToLower();

            if (name.Contains("di_chuyen"))
                return "⚡";

            if (name.Contains("chuot"))
                return "🖱️";

            return "⌨️";
        }

        public void LoadProcessedEvents(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("Hãy ấn xử lý dữ liệu trước");
                return; 
            }
                

            string[] files = Directory.GetFiles(folderPath, "*.csv");

            int i = 0;
            while (i < files.Length)
            {
                // Replace all usages of 'Path' with 'System.IO.Path' to resolve ambiguity

                string fileName = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                // VD: 00001_Di_chuyen_chuot_mouse

                string[] parts = fileName.Split('_', 2);
                if (parts.Length < 2)
                {
                    i++;
                    continue;
                }

                string index = parts[0];          // 00001
                string eventName = parts[1];      // Di_chuyen_chuot_mouse

                string icon = GetIconFromEventName(eventName);

                AddActionItem(
                    icon,
                    $"{index}_[{eventName}]",
                    "File CSV • Loaded"
                );

                i++;
            }
        }

        public void ShowReportMessageBox(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show(
                        "Không tìm thấy file báo cáo!",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string content = File.ReadAllText(filePath, Encoding.UTF8);

                MessageBox.Show(
                    content,
                    "Báo cáo thống kê",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Lỗi đọc file",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            
            LoadProcessedEvents("server/processed_events");
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            
            ShowReportMessageBox("server/Bao_cao_thong_ke.txt");
        }
        private bool ActionListFilter(object obj)
        {
            if (obj is not ActionItem item)
                return false;

            bool showKeyboard = KeyboardCheckBox.IsChecked == true;
            bool showMouse = MouseCheckBox.IsChecked == true;

            string lowerTitle = item.Title.ToLower();

            // Nếu chứa "nhan" -> bàn phím
            if (lowerTitle.Contains("nhan") && showKeyboard)
                return true;

            // Nếu chứa "chuot" -> chuột
            if (lowerTitle.Contains("chuot") && showMouse)
                return true;

            return false; // ẩn các item không được check
        }

        private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ActionListView == null)
                return;
            ActionListView.Refresh();
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            controlServer.SendCommand($"Sap_xep");
            //MessageBox.Show("Đã gửi lệnh sắp xếp đợi tối thiểu 2 để sever xử lý");
            await Task.Delay(2000); // Chờ 2 giây để server xử lý
            controlServer.SendCommand($"Tach_va_dich");
            //MessageBox.Show("Đã gửi lệnh xử lý kê tiếp tối thiểu 2 giây để sever xử lý");
            await Task.Delay(2000); // Chờ 2 giây để server xử lý
            controlServer.SendCommand($"Phan_tich");
            //MessageBox.Show("Đã gửi lệnh thống kê đợi tối thiểu 5 giây để sever xử lý");
            await Task.Delay(5000); // Chờ 2 giây để server xử lý
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            
            controlServer.DeleteFiles("server/processed", 2);
            controlServer.DeleteFiles("server/log", 2);
            
            controlServer.DeleteFiles("server/processed_events", 2);
            controlServer.DeleteFiles("server/Bao_cao_thong_ke.txt", 1);
        }
    }

}
