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
    // --- 1. CLASS DỮ LIỆU ĐA NĂNG (Xử lý cả 2 loại file) ---
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

        // Xử lý file từ Logger.exe (Format 10 cột)
        public static LogEvent FromProcessedCsv(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 9 || !long.TryParse(parts[3], out long t)) return null;
                return new LogEvent
                {
                    Time = t,
                    Type = parts[0].Trim() == "1" ? "KEYBOARD" : "MOUSE",
                    DecodedAction = parts[0].Trim() == "1"
                        ? DecodeKeyboard(ParseHex(parts[2]), (int)ParseHex(parts[4]))
                        : DecodeMouse(ParseHex(parts[2])) + (parts.Length >= 9 ? $" ({parts[7]}, {parts[8]})" : ""),
                    SourceFile = "merged_log.csv"
                };
            }
            catch { return null; }
        }

        // Xử lý file Log Thô trực tiếp (Format 6 cột) - Dùng khi Logger.exe lỗi
        public static LogEvent FromRawCsv(string filename, string line)
        {
            try
            {
                if (line.StartsWith("version") || line.StartsWith("Event")) return null;
                var parts = line.Split(',');
                if (parts.Length < 6) return null;

                // Keyboard Raw: 0:Idx, 1:MsgID, 2:Time, 3:Vk, 4:Scan, 5:Flags
                // Mouse Raw:    0:Idx, 1:MsgID, 2:Time, 3:X,  4:Y,    5:Data

                // Xác định loại dựa vào tên file
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

        private static uint ParseHex(string hex) { try { return Convert.ToUInt32(hex.Trim(), 16); } catch { return 0; } }
        private static string DecodeMouse(uint id) => id switch { 0x200 => "Di chuyển", 0x201 => "Click Trái", 0x202 => "Nhả Trái", 0x204 => "Click Phải", 0x205 => "Nhả Phải", 0x20A => "Cuộn chuột", _ => $"Mouse_{id:X}" };
        private static string DecodeKeyboard(uint id, int vk) => $"{(id == 0x100 ? "Nhấn" : "Nhả")} [{((WinForms.Keys)vk)}]";
    }

    // --- 2. LOGIC CHÍNH ---
    public partial class HOOK_UC_filter : System.Windows.Controls.UserControl
    {
        private List<LogEvent> _allEventsCache = new List<LogEvent>();
        public ObservableCollection<LogEvent> DisplayEvents { get; set; } = new ObservableCollection<LogEvent>();
        private const string EXE_FILENAME = "logger.exe";

        public HOOK_UC_filter()
        {
            InitializeComponent();
            if (lstEvents != null) lstEvents.ItemsSource = DisplayEvents;
            if (txtSearch != null) txtSearch.TextChanged += (s, e) => ApplyFilter();
            if (cbMouse != null) { cbMouse.Checked += (s, e) => ApplyFilter(); cbMouse.Unchecked += (s, e) => ApplyFilter(); }
            if (cbKeyboard != null) { cbKeyboard.Checked += (s, e) => ApplyFilter(); cbKeyboard.Unchecked += (s, e) => ApplyFilter(); }
        }

        // --- HÀM XỬ LÝ "THÔNG MINH" ---
        public async void ProcessAndLoadLogs(string sourceFolder)
        {
            if (!Directory.Exists(sourceFolder)) { System.Windows.MessageBox.Show("Thư mục lỗi."); return; }

            if (lblCount != null) lblCount.Text = "Đang xử lý...";
            DisplayEvents.Clear(); _allEventsCache.Clear();

            // CÁCH 1: THỬ DÙNG LOGGER.EXE (CHẾ ĐỘ SỬA LỖI)
            bool toolSuccess = await RunLoggerToolSafe(sourceFolder);

            // CÁCH 2: NẾU TOOL THẤT BẠI, DÙNG CODE C# ĐỌC TRỰC TIẾP (FALLBACK)
            if (!toolSuccess)
            {
                // MessageBox.Show("Logger.exe gặp lỗi (có thể do file rỗng). Đang chuyển sang chế độ đọc trực tiếp...", "Thông báo tự động");
                await LoadRawLogsDirectly(sourceFolder);
            }

            // Hiển thị kết quả
            _allEventsCache = _allEventsCache.OrderBy(x => x.Time).ToList();
            ApplyFilter();
            if (lblCount != null) lblCount.Text = $"Đã tải {_allEventsCache.Count} sự kiện.";

            if (_allEventsCache.Count == 0) System.Windows.MessageBox.Show("Không tìm thấy dữ liệu nào trong các file log.");
        }

        private async Task<bool> RunLoggerToolSafe(string sourceFolder)
        {
            return await Task.Run(() =>
            {
                string tempDir = "";
                try
                {
                    string exePath = Path.Combine(sourceFolder, EXE_FILENAME);
                    if (!File.Exists(exePath)) return false;

                    // Tạo môi trường Temp
                    tempDir = Path.Combine(Path.GetTempPath(), "LogTool_" + Guid.NewGuid());
                    Directory.CreateDirectory(tempDir);
                    File.Copy(exePath, Path.Combine(tempDir, EXE_FILENAME), true);

                    // Copy log và đổi tên (log0 -> log1). QUAN TRỌNG: BỎ QUA FILE RỖNG
                    var files = Directory.GetFiles(sourceFolder, "*.csv");
                    int kCount = 1, mCount = 1;

                    foreach (var f in files.OrderBy(n => n))
                    {
                        var info = new FileInfo(f);
                        if (info.Length < 100) continue; // Bỏ qua file rỗng (như log9)

                        if (f.Contains("keyboard")) File.Copy(f, Path.Combine(tempDir, $"keyboard_log{kCount++}.csv"), true);
                        if (f.Contains("mouse")) File.Copy(f, Path.Combine(tempDir, $"mouse_log{mCount++}.csv"), true);
                    }

                    Directory.CreateDirectory(Path.Combine(tempDir, "processed"));

                    // Chạy Tool
                    var procInfo = new ProcessStartInfo(Path.Combine(tempDir, EXE_FILENAME))
                    {
                        WorkingDirectory = tempDir,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = Process.Start(procInfo)) p.WaitForExit(5000);

                    // Đọc kết quả
                    string resultFile = Path.Combine(tempDir, "processed", "merged_log.csv");
                    if (File.Exists(resultFile))
                    {
                        var lines = File.ReadAllLines(resultFile);
                        foreach (var line in lines)
                        {
                            var evt = LogEvent.FromProcessedCsv(line);
                            if (evt != null) _allEventsCache.Add(evt);
                        }
                        return _allEventsCache.Count > 0;
                    }
                    return false;
                }
                catch { return false; }
                finally { try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { } }
            });
        }

        private async Task LoadRawLogsDirectly(string folder)
        {
            await Task.Run(() => {
                var files = Directory.GetFiles(folder, "*.csv");
                foreach (var f in files)
                {
                    if (Path.GetFileName(f) == "merged_log.csv") continue; // Bỏ qua file kết quả cũ
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
            });
        }

        private void ApplyFilter()
        {
            if (DisplayEvents == null) return;
            string kw = txtSearch?.Text.ToLower() ?? "";
            bool m = cbMouse?.IsChecked ?? true, k = cbKeyboard?.IsChecked ?? true;

            var result = _allEventsCache.Where(x => {
                if (x.Type == "MOUSE" && !m) return false;
                if (x.Type == "KEYBOARD" && !k) return false;
                return string.IsNullOrEmpty(kw) || (x.DecodedAction?.ToLower().Contains(kw) ?? false);
            });
            DisplayEvents.Clear();
            foreach (var item in result) DisplayEvents.Add(item);
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e) => ApplyFilter();
        private void BtnClear_Click(object sender, RoutedEventArgs e) { DisplayEvents.Clear(); _allEventsCache.Clear(); }
        private void BtnReplay_Click(object sender, RoutedEventArgs e) { System.Windows.MessageBox.Show("Coming soon..."); }
    }
}