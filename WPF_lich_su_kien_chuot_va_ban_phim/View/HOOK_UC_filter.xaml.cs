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

        // ==========================================

        // 1. CÁC THUỘC TÍNH CƠ BẢN

        // ==========================================

        public string Type { get; set; }            // "KEYBOARD" hoặc "MOUSE"

        public string DecodedAction { get; set; }   // Nội dung hiển thị (VD: "Nhấn A", "Ctrl + C")

        public long Time { get; set; }              // Timestamp

        public string SourceFile { get; set; }      // Tên file nguồn



        // ==========================================

        // 2. CÁC THUỘC TÍNH MỚI (HỖ TRỢ THUẬT TOÁN COMBO)

        // ==========================================

        public int RawVkCode { get; set; }          // Mã phím gốc (để kiểm tra Ctrl/Alt/Shift)

        public bool IsKeyDown { get; set; }         // Trạng thái: True = Nhấn, False = Nhả

        public bool IsCombo { get; set; } = false;  // Đánh dấu dòng này là tổ hợp phím



        // ==========================================

        // 3. CÁC THUỘC TÍNH HIỂN THỊ UI (BINDING)

        // ==========================================

        public string DisplayTime => Time.ToString();



        public string Icon => Type == "MOUSE" ? "🖱️" : "⌨️";



        public Brush IconBgColor => Type == "MOUSE" ? Brushes.AliceBlue : Brushes.Honeydew;



        public Brush TextColor

        {

            get

            {

                // Nếu là Combo -> Màu đỏ nổi bật

                if (IsCombo) return Brushes.Red;

                // Mặc định: Chuột màu xanh dương, Phím màu xanh lá

                return Type == "MOUSE" ? Brushes.RoyalBlue : Brushes.SeaGreen;

            }

        }



        // ==========================================

        // 4. PHƯƠNG THỨC XỬ LÝ FILE ĐẦU RA (MERGED LOG)

        // ==========================================

        // Format dự kiến từ logger.exe: 

        // Type(1=Key,0=Mouse), ID, MsgID(hex), Time, Vk(hex), Scan(hex), Flags(hex), X, Y, MouseData(hex)

        public static LogEvent FromProcessedCsv(string line, string sourceFolder = "")

        {

            try

            {

                var parts = line.Split(',');

                if (parts.Length < 9 || !long.TryParse(parts[3], out long t)) return null;



                bool isKey = parts[0].Trim() == "1";

                uint msgId = ParseHex(parts[2]);



                // Lấy thông tin bổ sung cho thuật toán

                int vkCode = isKey ? (int)ParseHex(parts[4]) : 0;



                // Xác định trạng thái Nhấn/Nhả dựa trên MsgID

                // 0x100: WM_KEYDOWN, 0x104: WM_SYSKEYDOWN

                // 0x201: WM_LBUTTONDOWN, 0x204: WM_RBUTTONDOWN

                bool isDown = (msgId == 0x100 || msgId == 0x104 || msgId == 0x201 || msgId == 0x204);



                string actionInfo = "";



                if (isKey)

                {

                    actionInfo = DecodeKeyboard(msgId, vkCode);

                }

                else

                {

                    actionInfo = DecodeMouse(msgId);

                    // Nếu có tọa độ X, Y

                    if (parts.Length >= 9) actionInfo += $" ({parts[7]}, {parts[8]})";

                }



                return new LogEvent

                {

                    Time = t,

                    Type = isKey ? "KEYBOARD" : "MOUSE",

                    DecodedAction = actionInfo,

                    SourceFile = string.IsNullOrEmpty(sourceFolder) ? "merged_log.csv" : sourceFolder,



                    // Gán dữ liệu cho thuật toán

                    RawVkCode = vkCode,

                    IsKeyDown = isDown

                };

            }

            catch { return null; }

        }



        // ==========================================

        // 5. PHƯƠNG THỨC XỬ LÝ FILE LOG THÔ (RAW)

        // ==========================================

        // Keyboard Raw: Idx, MsgID, Time, Vk, Scan, Flags

        // Mouse Raw:    Idx, MsgID, Time, X, Y, Data

        public static LogEvent FromRawCsv(string filename, string line)

        {

            try

            {

                // Bỏ qua header hoặc dòng rác

                if (line.StartsWith("version", StringComparison.OrdinalIgnoreCase) ||

                    line.StartsWith("Event", StringComparison.OrdinalIgnoreCase)) return null;



                var parts = line.Split(',');

                if (parts.Length < 4) return null; // Ít nhất phải có MsgID, Time



                bool isKey = filename.ToLower().Contains("key");

                uint msgId = ParseHex(parts[1]);

                long time = long.Parse(parts[2]);



                // Lấy thông tin bổ sung

                // Với file Raw Keyboard, Vk nằm ở index 3

                int vkCode = isKey ? (int)ParseHex(parts[3]) : 0;

                bool isDown = (msgId == 0x100 || msgId == 0x104 || msgId == 0x201 || msgId == 0x204);



                var evt = new LogEvent

                {

                    Time = time,

                    SourceFile = filename,

                    RawVkCode = vkCode,

                    IsKeyDown = isDown

                };



                if (isKey)

                {

                    evt.Type = "KEYBOARD";

                    evt.DecodedAction = DecodeKeyboard(msgId, vkCode);

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



        // ==========================================

        // 6. CÁC HÀM GIẢI MÃ (HELPER)

        // ==========================================

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

            0x207 => "Click Giữa",

            0x208 => "Nhả Giữa",

            0x20A => "Cuộn chuột",

            _ => $"Mouse_{id:X}"

        };



        private static string DecodeKeyboard(uint id, int vk)

        {

            // 0x100/0x104 là Nhấn, còn lại (0x101/0x105) là Nhả

            string trangThai = (id == 0x100 || id == 0x104) ? "Nhấn" : "Nhả";



            // Dùng thư viện WinForms để chuyển mã VK thành tên phím (VD: 65 -> A, 13 -> Enter)

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

            if (cbOnlyCombo != null)
            {
                cbOnlyCombo.Checked += (s, e) => ApplyFilter();
                cbOnlyCombo.Unchecked += (s, e) => ApplyFilter();
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

            bool logLoaded = await LoadEventLogs();
            bool reportLoaded = await LoadStatisticsReport();

            if (logLoaded)
            {
                // 1. Sắp xếp theo thời gian
                _allEventsCache = _allEventsCache.OrderBy(x => x.Time).ToList();

                // 2. --- GỌI HÀM XỬ LÝ COMBO Ở ĐÂY ---
                PostProcessCombos();

                // 3. Hiển thị ra giao diện
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

        private void PostProcessCombos()
        {
            // Các mã phím ảo (Virtual Key Codes) của phím bổ trợ
            const int VK_SHIFT = 16;
            const int VK_CONTROL = 17;
            const int VK_MENU = 18; // Alt
            const int VK_LWIN = 91;
            const int VK_RWIN = 92;

            bool isCtrl = false;
            bool isShift = false;
            bool isAlt = false;
            bool isWin = false;

            foreach (var evt in _allEventsCache)
            {
                if (evt.Type != "KEYBOARD") continue;

                // 1. Cập nhật trạng thái phím bổ trợ
                if (evt.RawVkCode == VK_SHIFT || evt.RawVkCode == 160 || evt.RawVkCode == 161)
                {
                    isShift = evt.IsKeyDown;
                    continue; // Không hiển thị riêng lẻ Shift nếu muốn gọn
                }
                if (evt.RawVkCode == VK_CONTROL || evt.RawVkCode == 162 || evt.RawVkCode == 163)
                {
                    isCtrl = evt.IsKeyDown;
                    continue;
                }
                if (evt.RawVkCode == VK_MENU || evt.RawVkCode == 164 || evt.RawVkCode == 165)
                {
                    isAlt = evt.IsKeyDown;
                    continue;
                }
                if (evt.RawVkCode == VK_LWIN || evt.RawVkCode == VK_RWIN)
                {
                    isWin = evt.IsKeyDown;
                    continue;
                }

                // 2. Nếu là phím thường VÀ đang giữ phím bổ trợ -> Đây là Combo
                if (evt.IsKeyDown && (isCtrl || isAlt || isWin || (isShift && IsSpecialKey(evt.RawVkCode))))
                {
                    List<string> comboParts = new List<string>();
                    if (isCtrl) comboParts.Add("Ctrl");
                    if (isAlt) comboParts.Add("Alt");
                    if (isShift) comboParts.Add("Shift");
                    if (isWin) comboParts.Add("Win");

                    // Lấy tên phím hiện tại (bỏ chữ "Nhấn [...]")
                    string keyName = ((WinForms.Keys)evt.RawVkCode).ToString();
                    comboParts.Add(keyName);

                    // Cập nhật lại nội dung hiển thị
                    evt.DecodedAction = string.Join(" + ", comboParts);
                    evt.IsCombo = true; // Để đổi màu hiển thị
                }
            }
        }

        // Hàm phụ trợ: Chỉ coi Shift là combo nếu đi cùng các phím chức năng hoặc phím đặc biệt
        // (Tránh việc Shift + A chỉ là viết hoa chữ A)
        private bool IsSpecialKey(int vk)
        {
            // F1-F12, Delete, Insert, Home, End, Tab...
            if (vk >= 112 && vk <= 123) return true; // F keys
            if (vk == 9 || vk == 46 || vk == 45 || vk == 36 || vk == 35) return true;
            return false;
            // Nếu muốn bắt tất cả Shift + Chữ cái thì return true luôn.
        }

        // ========== CÁC HÀM UI ==========
        private void ApplyFilter()
        {
            if (DisplayEvents == null) return;

            string kw = txtSearch?.Text.ToLower() ?? "";
            bool m = cbMouse?.IsChecked ?? true;
            bool k = cbKeyboard?.IsChecked ?? true;

            // --- LẤY TRẠNG THÁI NÚT MỚI ---
            bool onlyCombo = cbOnlyCombo?.IsChecked ?? false;

            var result = _allEventsCache.Where(x =>
            {
                // 1. Lọc theo loại thiết bị
                if (x.Type == "MOUSE" && !m) return false;
                if (x.Type == "KEYBOARD" && !k) return false;

                // 2. --- LOGIC LỌC COMBO MỚI ---
                // Nếu đang tích "Chỉ hiện Combo" mà dòng này KHÔNG phải Combo -> Ẩn luôn
                if (onlyCombo && !x.IsCombo) return false;

                // 3. Tìm kiếm từ khóa (giữ nguyên)
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

        //private void BtnShowReport_Click(object sender, RoutedEventArgs e)
        //{
        //    System.Windows.MessageBox.Show(_cachedReportContent, "BÁO CÁO THỐNG KÊ & COMBO",
        //        MessageBoxButton.OK, MessageBoxImage.Information);
        //}

        private async void BtnShowReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Kiểm tra xem có cần chạy lại tool không
                bool needToRunExe = string.IsNullOrEmpty(_cachedReportContent) ||
                                    _cachedReportContent.Contains("Chưa có dữ liệu") ||
                                    _cachedReportContent.Contains("Lỗi");

                if (needToRunExe)
                {
                    if (lblCount != null) lblCount.Text = "Đang chạy Logger.exe...";

                    bool success = await Task.Run(() =>
                    {
                        try
                        {
                            string exePath = Path.Combine(_serverFolder, EXE_FILENAME);

                            // DEBUG: Kiểm tra file exe có tồn tại không
                            if (!File.Exists(exePath))
                            {
                                Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Không tìm thấy file chạy tại:\n{exePath}", "Lỗi Đường Dẫn"));
                                return false;
                            }

                            // Cấu hình chạy Process
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = exePath,
                                WorkingDirectory = _serverFolder, // Chạy tại thư mục server
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };

                            using (var process = Process.Start(psi))
                            {
                                process?.WaitForExit(10000); // Tăng thời gian chờ lên 10s
                            }

                            // 2. TÌM KIẾM FILE BÁO CÁO (QUÉT TOÀN BỘ THƯ MỤC CON)
                            // Code cũ chỉ tìm ở gốc, code này tìm cả trong folder con như "processed"
                            string[] reportFiles = Directory.GetFiles(_serverFolder, "Bao_cao_thong_ke.txt", SearchOption.AllDirectories);

                            if (reportFiles.Length > 0)
                            {
                                // Lấy file mới nhất nếu có nhiều file
                                string bestFile = reportFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).First();
                                _cachedReportContent = File.ReadAllText(bestFile, System.Text.Encoding.UTF8);
                                return true;
                            }

                            return false;
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => System.Windows.MessageBox.Show("Lỗi khi chạy tool: " + ex.Message));
                            return false;
                        }
                    });

                    if (!success)
                    {
                        System.Windows.MessageBox.Show("Tool đã chạy nhưng không sinh ra file báo cáo.\nHãy kiểm tra lại thư mục 'server/log'.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (lblCount != null) lblCount.Text = "Lỗi tạo báo cáo.";
                        return;
                    }

                    if (lblCount != null) lblCount.Text = "Đã cập nhật báo cáo.";
                }

                // 3. Hiển thị báo cáo
                System.Windows.MessageBox.Show(
                    _cachedReportContent,
                    "BÁO CÁO THỐNG KÊ & COMBO",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Lỗi UI: " + ex.Message);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAllData();
        }


    }
}