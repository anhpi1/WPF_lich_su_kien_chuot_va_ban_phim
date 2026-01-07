using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;



namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    // ==========================================
    // 1. CLASS DỮ LIỆU (MODEL)
    // ==========================================
    public class LogEvent
    {
        public string Type { get; set; }
        public string DecodedAction { get; set; }
        public long Time { get; set; }
        public string SourceFile { get; set; }

        public string DisplayTime => Time.ToString();
        public string Icon => Type == "MOUSE" ? "🖱️" : "⌨️";
        public Brush IconBgColor => Type == "MOUSE" ? Brushes.AliceBlue : Brushes.Honeydew;
        public Brush TextColor => Type == "MOUSE" ? Brushes.RoyalBlue : Brushes.SeaGreen;

        // Xử lý dòng từ file kết quả chi tiết
        public static LogEvent FromDetailedCsv(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 4 || !long.TryParse(parts[3], out long t)) return null;

                bool isKey = parts[0].Trim() == "1";
                string actionInfo = "";

                if (isKey)
                    actionInfo = DecodeKeyboard(ParseHex(parts[2]), (int)ParseHex(parts[4]));
                else
                {
                    actionInfo = DecodeMouse(ParseHex(parts[2]));
                    if (parts.Length >= 9) actionInfo += $" ({parts[7]}, {parts[8]})";
                }

                return new LogEvent
                {
                    Time = t,
                    Type = isKey ? "KEYBOARD" : "MOUSE",
                    DecodedAction = actionInfo,
                    SourceFile = "Bang_chi_tiet_su_kien.csv"
                };
            }
            catch { return null; }
        }

        private static uint ParseHex(string hex) { try { return Convert.ToUInt32(hex.Trim(), 16); } catch { return 0; } }

        private static string DecodeMouse(uint id) => id switch
        {
            0x200 => "Di chuyển",
            0x201 => "Click Trái",
            0x202 => "Nhả Trái",
            0x204 => "Click Phải",
            0x205 => "Nhả Phải",
            0x20A => "Cuộn chuột",
            _ => $"Mouse_{id:X}"
        };

        private static string DecodeKeyboard(uint id, int vk)
        {
            string trangThai = (id == 0x100 || id == 0x104) ? "Nhấn" : "Nhả";
            string phim = ((WinForms.Keys)vk).ToString();
            return $"{trangThai} [{phim}]";
        }
    }

    // ==========================================
    // 2. CLASS GIAO DIỆN CHÍNH (VIEW)
    // ==========================================
    public partial class HOOK_UC_filter : System.Windows.Controls.UserControl
    {
        private List<LogEvent> _allEventsCache = new List<LogEvent>();
        public ObservableCollection<LogEvent> DisplayEvents { get; set; } = new ObservableCollection<LogEvent>();

        private const string EXE_FILENAME = "logger.exe";
        private string _serverPath; // Đường dẫn đến thư mục 'server'
        private string _logPath;    // Đường dẫn đến thư mục 'server/log'
        private string _cachedReportContent = "Chưa có dữ liệu thống kê.";

        public HOOK_UC_filter()
        {
            InitializeComponent();

            // 1. CẤU HÌNH ĐƯỜNG DẪN DỰA TRÊN ẢNH BẠN GỬI
            // App chạy tại .../bin/Debug/net8.0-windows/
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _serverPath = Path.Combine(baseDir, "server");
            _logPath = Path.Combine(_serverPath, "log");

            // Setup Binding UI
            if (lstEvents != null) lstEvents.ItemsSource = DisplayEvents;
            if (txtSearch != null) txtSearch.TextChanged += (s, e) => ApplyFilter();
            if (cbMouse != null) { cbMouse.Checked += (s, e) => ApplyFilter(); cbMouse.Unchecked += (s, e) => ApplyFilter(); }
            if (cbKeyboard != null) { cbKeyboard.Checked += (s, e) => ApplyFilter(); cbKeyboard.Unchecked += (s, e) => ApplyFilter(); }

            this.Loaded += HOOK_UC_filter_Loaded;
        }

        private void HOOK_UC_filter_Loaded(object sender, RoutedEventArgs e)
        {
            // Tự động chạy khi mở App
            ProcessAndLoadLogs();
        }

        public async void ProcessAndLoadLogs()
        {
            if (lblCount != null) lblCount.Text = "Đang xử lý dữ liệu...";
            DisplayEvents.Clear(); _allEventsCache.Clear();

            // Gọi hàm chạy Tool
            bool toolSuccess = await RunLoggerToolAndBypassWait();

            // Sắp xếp và hiển thị kết quả
            _allEventsCache = _allEventsCache.OrderBy(x => x.Time).ToList();
            ApplyFilter();

            if (lblCount != null)
                lblCount.Text = toolSuccess
                ? $"Đã tải {_allEventsCache.Count} sự kiện."
                : "Không có dữ liệu hoặc lỗi Tool.";
        }

        private async Task<bool> RunLoggerToolAndBypassWait()
        {
            return await Task.Run(() =>
            {
                string tempDir = "";
                try
                {
                    // --- BƯỚC 1: KIỂM TRA FILE GỐC (Validation) ---
                    if (!Directory.Exists(_serverPath)) return false;

                    string exeSource = Path.Combine(_serverPath, EXE_FILENAME);
                    if (!File.Exists(exeSource))
                    {
                        Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"LỖI: Không tìm thấy file chạy!\nApp đang tìm tại: {exeSource}"));
                        return false;
                    }
                    if (!Directory.Exists(_logPath)) Directory.CreateDirectory(_logPath);

                    // --- BƯỚC 2: TẠO MÔI TRƯỜNG TEMP (GIẢ LẬP CẤU TRÚC SERVER) ---
                    // Lý do dùng Temp: Để tránh lỗi "File đang được sử dụng" và không làm rác thư mục gốc.
                    tempDir = Path.Combine(Path.GetTempPath(), "LogTool_" + Guid.NewGuid());
                    Directory.CreateDirectory(tempDir); // Đây đóng vai trò là thư mục "server" giả

                    // Tạo thư mục con "log" bên trong Temp
                    string tempLogDir = Path.Combine(tempDir, "log");
                    Directory.CreateDirectory(tempLogDir);

                    // --- BƯỚC 3: COPY FILE VÀO TEMP ---

                    // A. Copy logger.exe và các file phụ trợ (dll, json) vào root Temp
                    File.Copy(exeSource, Path.Combine(tempDir, EXE_FILENAME), true);
                    foreach (var f in Directory.GetFiles(_serverPath, "*.dll")) File.Copy(f, Path.Combine(tempDir, Path.GetFileName(f)), true);
                    foreach (var f in Directory.GetFiles(_serverPath, "*.json")) File.Copy(f, Path.Combine(tempDir, Path.GetFileName(f)), true);

                    // B. Copy các file CSV từ server/log vào temp/log
                    // ĐẶC BIỆT: Đổi tên file về chuẩn "key.csv" và "mouse.csv" để Tool nhận diện
                    var csvFiles = Directory.GetFiles(_logPath, "*.csv");
                    bool kFound = false, mFound = false;

                    foreach (var f in csvFiles)
                    {
                        string fname = Path.GetFileName(f).ToLower();
                        // Bỏ qua các file kết quả cũ
                        if (fname.Contains("bang_chi_tiet") || fname.Contains("merged") || fname.Contains("bao_cao")) continue;

                        string destName = Path.GetFileName(f); // Mặc định giữ tên cũ

                        // Ưu tiên đổi tên file đầu tiên tìm thấy
                        if (fname.Contains("key") && !kFound) { destName = "key.csv"; kFound = true; }
                        else if (fname.Contains("mouse") && !mFound) { destName = "mouse.csv"; mFound = true; }

                        // Copy vào bên trong folder log của Temp
                        File.Copy(f, Path.Combine(tempLogDir, destName), true);
                    }

                    // --- BƯỚC 4: CHẠY TOOL ---
                    var procInfo = new ProcessStartInfo(Path.Combine(tempDir, EXE_FILENAME))
                    {
                        WorkingDirectory = tempDir, // Chạy tại root Temp (nơi có exe và folder log bên cạnh)
                        CreateNoWindow = true,      // Ẩn cửa sổ đen
                        UseShellExecute = false,
                        RedirectStandardInput = true
                    };

                    using (var p = Process.Start(procInfo))
                    {
                        p.StandardInput.WriteLine(); // Giả lập bấm Enter
                        p.WaitForExit(5000);         // Chờ tối đa 5s
                    }

                    // --- BƯỚC 5: ĐỌC KẾT QUẢ ---
                    // Theo ảnh của bạn, file kết quả sinh ra ngay cạnh file exe (tức là tại tempDir)

                    // 1. Đọc Báo Cáo Thống Kê (TXT)
                    string reportFile = Path.Combine(tempDir, "Bao_cao_thong_ke.txt");
                    // Kiểm tra dự phòng trong folder processed nếu tool đổi nết
                    if (!File.Exists(reportFile)) reportFile = Path.Combine(tempDir, "processed", "Bao_cao_thong_ke.txt");

                    // [QUAN TRỌNG] Fallback: Nếu Temp lỗi, thử tìm file báo cáo cũ ở thư mục gốc (server)
                    if (!File.Exists(reportFile)) reportFile = Path.Combine(_serverPath, "Bao_cao_thong_ke.txt");

                    if (File.Exists(reportFile))
                    {
                        string content = File.ReadAllText(reportFile);
                        Dispatcher.Invoke(() => _cachedReportContent = content);
                    }

                    // 2. Đọc Bảng Chi Tiết (CSV)
                    string resultCsv = Path.Combine(tempDir, "Bang_chi_tiet_su_kien.csv");
                    if (!File.Exists(resultCsv)) resultCsv = Path.Combine(tempDir, "processed", "merged_log.csv");
                    // Fallback về thư mục gốc
                    if (!File.Exists(resultCsv)) resultCsv = Path.Combine(_serverPath, "Bang_chi_tiet_su_kien.csv");

                    if (File.Exists(resultCsv))
                    {
                        var lines = File.ReadAllLines(resultCsv);
                        foreach (var line in lines)
                        {
                            var evt = LogEvent.FromDetailedCsv(line);
                            if (evt != null) _allEventsCache.Add(evt);
                        }
                        return _allEventsCache.Count > 0;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => System.Windows.MessageBox.Show("Lỗi xử lý: " + ex.Message));
                    return false;
                }
                finally
                {
                    // Dọn dẹp thư mục Temp sau khi xong
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        // --- CÁC HÀM UI ---
        private void ApplyFilter()
        {
            if (DisplayEvents == null) return;
            string kw = txtSearch?.Text.ToLower() ?? "";
            bool m = cbMouse?.IsChecked ?? true;
            bool k = cbKeyboard?.IsChecked ?? true;

            var result = _allEventsCache.Where(x => {
                if (x.Type == "MOUSE" && !m) return false;
                if (x.Type == "KEYBOARD" && !k) return false;
                return string.IsNullOrEmpty(kw) || (x.DecodedAction?.ToLower().Contains(kw) ?? false);
            });

            DisplayEvents.Clear();
            foreach (var item in result) DisplayEvents.Add(item);
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e) => ApplyFilter();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            DisplayEvents.Clear();
            _allEventsCache.Clear();
            if (lblCount != null) lblCount.Text = "Đã xóa dữ liệu hiển thị.";
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Chức năng Replay chưa khả dụng.", "Thông báo");
        }

        private void BtnShowReport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(_cachedReportContent, "Báo Cáo Thống Kê & Combo");
        }
    }
}