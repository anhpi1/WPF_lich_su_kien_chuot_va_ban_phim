using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Để dùng ObservableCollection
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{

    public class LogEvent

    {

        public string Type { get; set; } // "KEYBOARD" hoặc "MOUSE"

        public string DecodedAction { get; set; } // Hành động (VD: Nhan_Enter)

        public long Time { get; set; }

        public string SourceFile { get; set; } // Tên file nguồn (VD: keyboard_log1.csv)

        public string RawData { get; set; } // Dữ liệu gốc để debug



        // Thuộc tính hiển thị lên giao diện (Binding)

        public string DisplayTime => DateTimeOffset.FromUnixTimeMilliseconds(Time).LocalDateTime.ToString("HH:mm:ss.fff");



        public string Icon => Type == "MOUSE" ? "🖱️" : "⌨️";



        public Brush IconBgColor => Type == "MOUSE"

            ? new SolidColorBrush(Color.FromRgb(238, 242, 255))  // Xanh nhạt cho chuột

            : new SolidColorBrush(Color.FromRgb(240, 253, 244)); // Xanh lá nhạt cho phím



        public Brush TextColor => Type == "MOUSE"

            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))    // Xanh đậm

            : new SolidColorBrush(Color.FromRgb(22, 163, 74));   // Xanh lá đậm



        // Hàm parse dòng CSV (Logic gốc từ logger.exe)

        // Trong file LogEvent.cs

        public static LogEvent FromCsv(string filename, string line)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line)) return null;

                var parts = line.Split(',');
                if (parts.Length < 3) return null;

                // --- SỬA LỖI Ở ĐÂY: Bỏ qua dòng tiêu đề ---
                // Nếu cột đầu tiên là chữ "Type" hoặc "Event", hoặc cột Time không phải số -> Đây là Header, bỏ qua ngay
                if (line.StartsWith("Type") || line.StartsWith("Event") || !long.TryParse(parts[3], out long timeValue))
                {
                    return null;
                }

                var evt = new LogEvent
                {
                    SourceFile = filename,
                    RawData = line,
                    Time = timeValue // Đã parse an toàn ở trên
                };

                // Parse các thông số Hex (MsgID)
                // Dùng TryParse để tránh lỗi nếu dữ liệu bị rỗng
                uint msgId = 0;
                try
                {
                    // Xử lý cả trường hợp có tiền tố "0x" hoặc không
                    string hexMsg = parts[2].Replace("0x", "").Trim();
                    msgId = Convert.ToUInt32(hexMsg, 16);
                }
                catch { return null; }

                // Xác định loại (Keyboard hay Mouse)
                // File của bạn có header là "Event(uint)..." nên ta dựa vào tên file hoặc số lượng cột
                string rawType = parts[0].Trim();

                // Nếu tên file chứa "key" HOẶC dòng dữ liệu có đủ cột cho phím (VkCode, ScanCode...)
                if (filename.ToLower().Contains("key") || parts.Length >= 6)
                {
                    evt.Type = "KEYBOARD";
                    int vkCode = 0;
                    if (parts.Length > 4)
                    {
                        try { vkCode = Convert.ToInt32(parts[4].Replace("0x", "").Trim(), 16); } catch { }
                    }
                    evt.DecodedAction = DecodeKeyboard(msgId, vkCode);
                }
                else
                {
                    evt.Type = "MOUSE";
                    evt.DecodedAction = DecodeMouse(msgId);
                }

                if (string.IsNullOrEmpty(evt.DecodedAction)) return null;

                return evt;
            }
            catch
            {
                return null;
            }
        }



        // --- Logic dịch mã từ logger.exe ---

        private static string DecodeMouse(uint msgId)

        {

            switch (msgId)

            {

                case 0x201: return "Nhan_chuot_trai (Click)";

                case 0x202: return "Nha_chuot_trai";

                case 0x204: return "Nhan_chuot_phai";

                case 0x205: return "Nha_chuot_phai";

                case 0x20A: return "Cuon_chuot";

                // case 0x200: return "Di_chuyen_chuot"; // Bỏ comment nếu muốn hiện move

                default: return "";

            }

        }



        private static string DecodeKeyboard(uint msgId, int vkCode)

        {

            // Chỉ bắt sự kiện nhấn xuống (WM_KEYDOWN = 0x100, SYSKEYDOWN = 0x104)

            if (msgId != 0x100 && msgId != 0x104) return "";



            switch (vkCode)

            {

                case 0x0D: return "Nhan_Enter";

                case 0x20: return "Nhan_Space";

                case 0x08: return "Nhan_Backspace";

                case 0x09: return "Nhan_Tab";

                case 0x1B: return "Nhan_Esc";

                case 0x10: case 0xA0: case 0xA1: return "Nhan_Shift";

                case 0x11: case 0xA2: case 0xA3: return "Nhan_Ctrl";

                default:

                    if ((vkCode >= 0x30 && vkCode <= 0x39) || (vkCode >= 0x41 && vkCode <= 0x5A))

                        return $"Nhan_Phim_{(char)vkCode}";

                    return $"Key_{vkCode:X}";

            }

        }

    }



    public partial class HOOK_UC_filter : UserControl

    {

        // Danh sách gốc chứa toàn bộ dữ liệu từ file

        private List<LogEvent> _allEventsCache = new List<LogEvent>();



        // Danh sách hiển thị lên giao diện (đã qua lọc)

        public ObservableCollection<LogEvent> DisplayEvents { get; set; } = new ObservableCollection<LogEvent>();



        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;



        public HOOK_UC_filter()

        {

            InitializeComponent();



            // Gán DataContext hoặc ItemsSource

            lstEvents.ItemsSource = DisplayEvents;



            this.Loaded += HOOK_UC_filter_Loaded;



            // Sự kiện tìm kiếm: Gõ là lọc luôn

            txtSearch.TextChanged += (s, e) => ApplyFilter();

            cbMouse.Checked += (s, e) => ApplyFilter();

            cbMouse.Unchecked += (s, e) => ApplyFilter();

            cbKeyboard.Checked += (s, e) => ApplyFilter();

            cbKeyboard.Unchecked += (s, e) => ApplyFilter();

        }



        private void HOOK_UC_filter_Loaded(object sender, RoutedEventArgs e)

        {

            if (Math.Abs(ScaleFactor - 1.0) > 0.01) ApplyScale(this);

        }



        // --- HÀM CHÍNH: GỌI TỪ MAIN WINDOW ---

        // Trong file HOOK_UC_filter.xaml.cs

        public async void LoadLogsFromFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show("Thư mục không tồn tại!");
                return;
            }

            _allEventsCache.Clear();
            DisplayEvents.Clear();
            lblCount.Text = "Đang tải...";

            await Task.Run(() =>
            {
                try
                {
                    // Lấy tất cả file CSV (bất kể tên gì)
                    var files = Directory.GetFiles(folderPath, "*.csv");

                    if (files.Length == 0)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show("Không tìm thấy file .csv nào!"));
                        return;
                    }

                    foreach (var file in files)
                    {
                        // Tùy chọn: Chỉ đọc file có chữ "log" hoặc "mouse"/"key" để tránh đọc nhầm file rác
                        // if (!file.ToLower().Contains("log")) continue; 

                        var lines = File.ReadAllLines(file);

                        foreach (var line in lines) // KHÔNG DÙNG .Skip(1) NỮA
                        {
                            // Hàm FromCsv mới đã tự động check header, nên cứ truyền hết vào
                            var evt = LogEvent.FromCsv(System.IO.Path.GetFileName(file), line);
                            if (evt != null)
                            {
                                _allEventsCache.Add(evt);
                            }
                        }
                    }

                    // Sắp xếp lại theo thời gian
                    _allEventsCache = _allEventsCache.OrderBy(x => x.Time).ToList();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Lỗi: " + ex.Message));
                }
            });

            // Cập nhật giao diện
            Dispatcher.Invoke(() =>
            {
                if (_allEventsCache.Count == 0)
                    MessageBox.Show("Không đọc được sự kiện nào. Hãy kiểm tra lại file log.");

                ApplyFilter();
            });
        }



        // --- LOGIC LỌC ---

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)

        {

            ApplyFilter();

        }



        private void ApplyFilter()

        {

            string keyword = txtSearch.Text.ToLower();

            bool showMouse = cbMouse.IsChecked == true;

            bool showKey = cbKeyboard.IsChecked == true;



            var filtered = _allEventsCache.Where(ev =>

            {

                // 1. Lọc thiết bị

                if (ev.Type == "MOUSE" && !showMouse) return false;

                if (ev.Type == "KEYBOARD" && !showKey) return false;



                // 2. Lọc từ khóa

                if (!string.IsNullOrEmpty(keyword))

                {

                    if (!ev.DecodedAction.ToLower().Contains(keyword) &&

                        !ev.SourceFile.ToLower().Contains(keyword))

                        return false;

                }



                return true;

            }).ToList();



            // Đẩy vào ObservableCollection để UI tự cập nhật

            DisplayEvents.Clear();

            foreach (var item in filtered)

            {

                DisplayEvents.Add(item);

            }



            lblCount.Text = $"{DisplayEvents.Count} sự kiện";

        }



        // --- CÁC NÚT CHỨC NĂNG ---

        private void BtnClear_Click(object sender, RoutedEventArgs e)

        {

            _allEventsCache.Clear();

            DisplayEvents.Clear();

            lblCount.Text = "0 sự kiện";

        }



        private async void BtnReplay_Click(object sender, RoutedEventArgs e)

        {

            if (DisplayEvents.Count == 0) return;

            var btn = sender as Button;

            if (btn == null) return;



            btn.IsEnabled = false;

            btn.Content = "Đang chạy...";



            // Demo Replay: Highlight từng dòng

            foreach (var evt in DisplayEvents)

            {

                lstEvents.SelectedItem = evt;

                lstEvents.ScrollIntoView(evt);

                await Task.Delay(50); // Tốc độ replay

            }



            btn.Content = "Phát lại (Replay)";

            btn.IsEnabled = true;

            MessageBox.Show("Hoàn tất phát lại!", "Thông báo");

        }



        // --- UI SCALING ---

        private void ApplyScale(FrameworkElement parent)

        {

            double factor = ScaleFactor;

            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(parent).OfType<FrameworkElement>())

            {

                if (!double.IsNaN(child.Width)) child.Width *= factor;

                if (!double.IsNaN(child.Height)) child.Height *= factor;

                child.Margin = new Thickness(

                    child.Margin.Left * factor, child.Margin.Top * factor,

                    child.Margin.Right * factor, child.Margin.Bottom * factor);



                if (child is Panel || child is ContentControl || child is UserControl)

                    ApplyScale(child);

            }

        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)

        {



            var dialog = new Microsoft.Win32.OpenFileDialog();

            dialog.Title = "Chọn một file log bất kỳ trong thư mục";

            dialog.Filter = "CSV Files|*.csv|All Files|*.*";



            if (dialog.ShowDialog() == true)

            {

                // Lấy đường dẫn thư mục chứa file vừa chọn

                string folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);



                MyFilterUC.LoadLogsFromFolder(folderPath);

            }


        }


    }


}