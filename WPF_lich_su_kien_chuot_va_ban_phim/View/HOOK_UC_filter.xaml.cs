using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    // 1. CLASS DỮ LIỆU CHUNG
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

        // ========== PHƯƠNG THỨC CHO FILE KẾT QUẢ TỪ LOGGER.EXE (ĐƯỜNG DẪN ĐỘC LẬP) ==========
        public static LogEvent FromProcessedCsv(string line, string sourceFolder = "")
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 9 || !long.TryParse(parts[3], out long t)) return null;

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
                    SourceFile = string.IsNullOrEmpty(sourceFolder) ? "merged_log.csv" : sourceFolder
                };
            }
            catch { return null; }
        }

        // ========== PHƯƠNG THỨC CHO FILE LOG THÔ (ĐƯỜNG DẪN ĐỘC LẬP) ==========
        public static LogEvent FromRawCsv(string filename, string line)
        {
            try
            {
                if (line.StartsWith("version") || line.StartsWith("Event")) return null;
                var parts = line.Split(',');
                if (parts.Length < 6) return null;

                // Keyboard Raw: 0:Idx, 1:MsgID, 2:Time, 3:Vk, 4:Scan, 5:Flags
                // Mouse Raw:    0:Idx, 1:MsgID, 2:Time, 3:X,  4:Y,   5:Data

                bool isKey = filename.ToLower().Contains("key");
                uint msgId = ParseHex(parts[1]);
                long time = long.Parse(parts[2]);

                var evt = new LogEvent { Time = time, SourceFile = filename };

                if (isKey)
                {
                    evt.Type = "KEYBOARD";
                    evt.DecodedAction = DecodeKeyboard(msgId, (int)ParseHex(parts[3]));
                }
                else
                {
                    evt.Type = "MOUSE";
                    evt.DecodedAction = DecodeMouse(msgId);
                    if (parts.Length >= 5) evt.DecodedAction += $" ({parts[3]}, {parts[4]})";
                }
                return evt;
            }
            catch { return null; }
        }

        private static uint ParseHex(string hex)
        {
            try { return Convert.ToUInt32(hex.Trim(), 16); }
            catch { return 0; }
        }

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
    // 2. CLASS GIAO DIỆN CHÍNH
    // ==========================================
    public partial class HOOK_UC_filter : System.Windows.Controls.UserControl
    {
        private List<LogEvent> _allEventsCache = new List<LogEvent>();
        public ObservableCollection<LogEvent> DisplayEvents { get; set; } = new ObservableCollection<LogEvent>();

        private const string EXE_FILENAME = "logger.exe";
        private string _logFolder;          // Thư mục log gốc
        private string _serverFolder;       // Thư mục server chứa exe
        private string _cachedReportContent = "Chưa có dữ liệu thống kê.";

        public HOOK_UC_filter()
        {
            InitializeComponent();

            // THIẾT LẬP ĐƯỜNG DẪN ĐỘC LẬP
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _serverFolder = Path.Combine(baseDir, "server");      // Đường dẫn độc lập cho exe
            _logFolder = Path.Combine(_serverFolder, "log");      // Đường dẫn độc lập cho log

            // Setup Binding UI
            if (lstEvents != null) lstEvents.ItemsSource = DisplayEvents;
            if (txtSearch != null) txtSearch.TextChanged += (s, e) => ApplyFilter();
            if (cbMouse != null)
            {
                cbMouse.Checked += (s, e) => ApplyFilter();
                cbMouse.Unchecked += (s, e) => ApplyFilter();
            }
            if (cbKeyboard != null)
            {
                cbKeyboard.Checked += (s, e) => ApplyFilter();
                cbKeyboard.Unchecked += (s, e) => ApplyFilter();
            }

            this.Loaded += HOOK_UC_filter_Loaded;
        }

        private void HOOK_UC_filter_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllData();
        }

        // ========== HÀM TẢI TOÀN BỘ DỮ LIỆU ==========
        private async void LoadAllData()
        {
            if (lblCount != null) lblCount.Text = "Đang xử lý dữ liệu...";
            DisplayEvents.Clear();
            _allEventsCache.Clear();

            // XỬ LÝ ĐỘC LẬP: Nhật ký sự kiện
            bool logLoaded = await LoadEventLogs();

            // XỬ LÝ ĐỘC LẬP: Báo cáo thống kê
            bool reportLoaded = await LoadStatisticsReport();

            // Hiển thị kết quả
            if (logLoaded)
            {
                _allEventsCache = _allEventsCache.OrderBy(x => x.Time).ToList();
                ApplyFilter();
            }

            if (lblCount != null)
            {
                string status = $"Sự kiện: {_allEventsCache.Count} | ";
                status += reportLoaded ? "Đã tải báo cáo" : "Không có báo cáo";
                lblCount.Text = status;
            }

            if (!logLoaded && !reportLoaded)
            {
                System.Windows.MessageBox.Show("Không tìm thấy dữ liệu nào trong các file log.");
            }
        }

        // ========== XỬ LÝ ĐỘC LẬP 1: NHẬT KÝ SỰ KIỆN ==========
        private async Task<bool> LoadEventLogs()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Kiểm tra đường dẫn log
                    if (!Directory.Exists(_logFolder))
                    {
                        Dispatcher.Invoke(() =>
                            System.Windows.MessageBox.Show($"Không tìm thấy thư mục log:\n{_logFolder}"));
                        return false;
                    }

                    // PHƯƠNG PHÁP 1: DÙNG LOGGER.EXE (ƯU TIÊN)
                    bool toolSuccess = TryProcessWithLoggerExe();

                    // PHƯƠNG PHÁP 2: ĐỌC TRỰC TIẾP FILE THÔ (FALLBACK)
                    if (!toolSuccess)
                    {
                        LoadRawLogsDirectly();
                    }

                    return _allEventsCache.Count > 0;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"Lỗi tải nhật ký: {ex.Message}"));
                    return false;
                }
            });
        }

        private bool TryProcessWithLoggerExe()
        {
            string tempDir = "";
            try
            {
                // KIỂM TRA ĐƯỜNG DẪN ĐỘC LẬP CHO EXE
                string exePath = Path.Combine(_serverFolder, EXE_FILENAME);
                if (!File.Exists(exePath))
                {
                    return false;
                }

                // TẠO MÔI TRƯỜNG TEMP ĐỘC LẬP
                tempDir = Path.Combine(Path.GetTempPath(), "LogTool_" + Guid.NewGuid());
                Directory.CreateDirectory(tempDir);

                // SAO CHÉP EXE VÀ CÁC FILE PHỤ TRỢ
                File.Copy(exePath, Path.Combine(tempDir, EXE_FILENAME), true);

                // SAO CHÉP LOG VỚI ĐƯỜNG DẪN ĐỘC LẬP
                var files = Directory.GetFiles(_logFolder, "*.csv");
                int kCount = 1, mCount = 1;

                foreach (var f in files.OrderBy(n => n))
                {
                    var info = new FileInfo(f);
                    if (info.Length < 100) continue;

                    if (f.ToLower().Contains("keyboard"))
                        File.Copy(f, Path.Combine(tempDir, $"keyboard_log{kCount++}.csv"), true);
                    if (f.ToLower().Contains("mouse"))
                        File.Copy(f, Path.Combine(tempDir, $"mouse_log{mCount++}.csv"), true);
                }

                Directory.CreateDirectory(Path.Combine(tempDir, "processed"));

                // CHẠY TOOL VỚI ĐƯỜNG DẪN TEMP ĐỘC LẬP
                var procInfo = new ProcessStartInfo(Path.Combine(tempDir, EXE_FILENAME))
                {
                    WorkingDirectory = tempDir,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var p = Process.Start(procInfo))
                    p.WaitForExit(5000);

                // ĐỌC KẾT QUẢ TỪ ĐƯỜNG DẪN TEMP
                string resultFile = Path.Combine(tempDir, "processed", "merged_log.csv");
                if (File.Exists(resultFile))
                {
                    var lines = File.ReadAllLines(resultFile);
                    foreach (var line in lines)
                    {
                        var evt = LogEvent.FromProcessedCsv(line, "Logger Tool Output");
                        if (evt != null) _allEventsCache.Add(evt);
                    }
                    return _allEventsCache.Count > 0;
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                // DỌN DẸP TEMP
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }

        private void LoadRawLogsDirectly()
        {
            var files = Directory.GetFiles(_logFolder, "*.csv");
            foreach (var f in files)
            {
                if (Path.GetFileName(f).ToLower().Contains("bang_chi_tiet") ||
                    Path.GetFileName(f).ToLower().Contains("merged"))
                    continue;

                try
                {
                    var lines = File.ReadAllLines(f);
                    foreach (var line in lines)
                    {
                        var evt = LogEvent.FromRawCsv(Path.GetFileName(f), line);
                        if (evt != null) _allEventsCache.Add(evt);
                    }
                }
                catch { }
            }
        }

        // ========== XỬ LÝ ĐỘC LẬP 2: BÁO CÁO THỐNG KÊ ==========
        private async Task<bool> LoadStatisticsReport()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // KIỂM TRA ĐƯỜNG DẪN ĐỘC LẬP CHO BÁO CÁO
                    string reportFile = "";

                    // ƯU TIÊN 1: File báo cáo trong server folder
                    reportFile = Path.Combine(_serverFolder, "Bao_cao_thong_ke.txt");

                    // ƯU TIÊN 2: File trong log folder
                    if (!File.Exists(reportFile))
                        reportFile = Path.Combine(_logFolder, "Bao_cao_thong_ke.txt");

                    // ƯU TIÊN 3: Tìm trong toàn bộ thư mục server
                    if (!File.Exists(reportFile))
                    {
                        var allReportFiles = Directory.GetFiles(_serverFolder, "Bao_cao_thong_ke.txt", SearchOption.AllDirectories);
                        if (allReportFiles.Length > 0)
                            reportFile = allReportFiles[0];
                    }

                    if (File.Exists(reportFile))
                    {
                        _cachedReportContent = File.ReadAllText(reportFile, System.Text.Encoding.UTF8);
                        return true;
                    }
                    else
                    {
                        _cachedReportContent = "Không tìm thấy file báo cáo thống kê.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _cachedReportContent = $"Lỗi đọc báo cáo: {ex.Message}";
                    return false;
                }
            });
        }

        // ========== CÁC HÀM UI ==========
        private void ApplyFilter()
        {
            if (DisplayEvents == null) return;

            string kw = txtSearch?.Text.ToLower() ?? "";
            bool m = cbMouse?.IsChecked ?? true;
            bool k = cbKeyboard?.IsChecked ?? true;

            var result = _allEventsCache.Where(x =>
            {
                if (x.Type == "MOUSE" && !m) return false;
                if (x.Type == "KEYBOARD" && !k) return false;
                return string.IsNullOrEmpty(kw) ||
                       (x.DecodedAction?.ToLower().Contains(kw) ?? false) ||
                       (x.SourceFile?.ToLower().Contains(kw) ?? false);
            });

            DisplayEvents.Clear();
            foreach (var item in result)
                DisplayEvents.Add(item);
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e) => ApplyFilter();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            DisplayEvents.Clear();
            _allEventsCache.Clear();
            _cachedReportContent = "Đã xóa dữ liệu.";
            if (lblCount != null)
                lblCount.Text = "Đã xóa dữ liệu.";
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Chức năng Replay đang phát triển...", "Thông báo");
        }

        private void BtnShowReport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(_cachedReportContent, "BÁO CÁO THỐNG KÊ & COMBO",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAllData();
        }
    }
}