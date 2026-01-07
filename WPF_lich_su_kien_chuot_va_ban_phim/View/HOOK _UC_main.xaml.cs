using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using System.Runtime.InteropServices;




namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    /// <summary>
    /// Interaction logic for HOOK__UC_main.xaml
    /// </summary>
    public partial class HOOK__UC_main : UserControl
    {
        private control_server_class controlServer;
        private string Selected_file_replay = "log\\mouse_log0.csv log\\keyboard_log0.csv";
        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;
        private bool togger_record = false;
        private bool togger_replay = false;
        private CancellationTokenSource? _replayCts;
        private const string LOG_ALIAS = "log\\";
        private const string REAL_LOG_FOLDER = @"server\log\";  // thư mục thật đang chứa file

        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(
    uint wVirtKey,
    uint wScanCode,
    byte[] lpKeyState,
    [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
    int cchBuff,
    uint wFlags,
    IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);
        private static string NormalizeSlashes(string p)
        {
            return (p ?? "").Trim().Replace('/', '\\');
        }

        // Nhận vào "log\keyboard_log18.csv" và trả về "server\log\keyboard_log18.csv"
        private static string MapLegacyLogPath(string legacyPath)
        {
            legacyPath = NormalizeSlashes(legacyPath);

            if (string.IsNullOrWhiteSpace(legacyPath))
                return legacyPath;

            // Nếu đã là đường dẫn tuyệt đối thì giữ nguyên
            if (System.IO.Path.IsPathRooted(legacyPath))
                return legacyPath;

            // Nếu bắt đầu bằng "log\" thì thay bằng "server\log\"
            if (legacyPath.StartsWith(LOG_ALIAS, StringComparison.OrdinalIgnoreCase))
            {
                string fileName = legacyPath.Substring(LOG_ALIAS.Length); // keyboard_log18.csv
                return System.IO.Path.Combine(REAL_LOG_FOLDER, fileName);
            }

            // Trường hợp khác: cứ coi là relative từ thư mục chạy
            return legacyPath;
        }



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
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
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
        private void FilePairListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
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
            try
            {
                _replayCts?.Cancel();
                _replayCts = new CancellationTokenSource();

                string selected = FilePairListBox.SelectedItem as string ?? "";
                selected = selected.Trim();

                if (string.IsNullOrWhiteSpace(selected))
                {
                    ReplayKeyboardText.Text = "Bạn chưa chọn cặp file log trong danh sách.";
                    return;
                }

                // Chuỗi dạng: "log\\mouse_log0.csv log\\keyboard_log0.csv"
                var parts = selected.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Lấy legacy path từ item
                string keyboardLegacy = parts.FirstOrDefault(p => p.ToLower().Contains("keyboard")) ?? "";
                if (string.IsNullOrWhiteSpace(keyboardLegacy))
                {
                    ReplayKeyboardText.Text = "Không tìm thấy đường dẫn keyboard trong item đã chọn.";
                    return;
                }

                // ✅ Map sang đường dẫn thật
                string keyboardPath = MapLegacyLogPath(keyboardLegacy);

                // Debug để bạn nhìn thấy map đúng chưa
                ReplayKeyboardText.Text =
                    $"Legacy keyboard: {keyboardLegacy}\n" +
                    $"Mapped keyboard:  {keyboardPath}\n";

                if (Mode1.IsChecked == true) // Keyboard
                {
                    await ReplayKeyboardAndShowAsync(keyboardPath, _replayCts.Token);
                }
                else if (Mode2.IsChecked == true) // Combine
                {
                    _ = ReplayKeyboardAndShowAsync(keyboardPath, _replayCts.Token);

                    // TODO: gọi replay mouse theo engine của bạn (nếu có)
                }
                else
                {
                    ReplayKeyboardText.Text = "Mouse mode: không hiển thị keyboard.";
                }
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => ReplayKeyboardText.AppendText("----- CANCELED -----\n"));
            }
            catch (Exception ex)
            {
                ReplayKeyboardText.Text = "Lỗi REPLAY: " + ex.Message;
            }
        }




        private static List<KeyboardReplayEvent> LoadKeyboardCsv(string path, out DateTime? startTime)
        {
            startTime = null;
            var lines = File.ReadAllLines(path);

            if (lines.Length < 3)
                return new List<KeyboardReplayEvent>();

            // 1) Parse metadata line (optional)
            // version,1,startTime,2025-12-30 14:25:47.645,screenWidth,1920,screenHeight,1080
            var meta = lines[0].Split(',');
            for (int i = 0; i < meta.Length - 1; i++)
            {
                if (meta[i].Trim().Equals("startTime", StringComparison.OrdinalIgnoreCase))
                {
                    var s = meta[i + 1].Trim();
                    if (DateTime.TryParseExact(
                            s,
                            "yyyy-MM-dd HH:mm:ss.fff",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var dt))
                    {
                        startTime = dt;
                    }
                    break;
                }
            }

            // 2) Skip header line (lines[1]) and parse data from lines[2..]
            var result = new List<KeyboardReplayEvent>();

            for (int idx = 2; idx < lines.Length; idx++)
            {
                var line = lines[idx].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                // Note: file của bạn đang là:
                // Event(uint),MsgID(hex),Time(uint),VkCode(hex),ScanCode(hex),Flags(hex)
                // nhưng data lại: 0,100,522...,32,3,0 (MsgID và VkCode có thể đang lưu dạng decimal hoặc hex không prefix)
                // => Ta parse linh hoạt: nếu có chữ a-f thì parse hex, không thì decimal.

                uint ParseAuto(string s)
                {
                    s = s.Trim();
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        return Convert.ToUInt32(s.Substring(2), 16);

                    bool looksHex = s.Any(ch => (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'));
                    return looksHex ? Convert.ToUInt32(s, 16) : Convert.ToUInt32(s, 10);
                }

                uint ParseHex(string s)
                {
                    s = (s ?? "").Trim();
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        s = s.Substring(2);
                    return Convert.ToUInt32(s, 16);
                }

                int ParseDec(string s)
                {
                    return (int)Convert.ToUInt32((s ?? "").Trim(), 10);
                }

                try
                {
                    // Replace 'unit' with 'uint' in the following lines:
                    var ev = new KeyboardReplayEvent
                    {
                        EventIndex = (uint)ParseDec(parts[0]),   // Event(uint) -> decimal
                        MsgId = ParseHex(parts[1]),              // MsgID(hex)  -> hex (100 => 0x100)
                        Time = (uint)ParseDec(parts[2]),         // Time(uint)  -> decimal
                        VkCode = ParseHex(parts[3]),             // VkCode(hex) -> hex
                        ScanCode = ParseHex(parts[4]),           // ScanCode(hex)-> hex
                        Flags = ParseHex(parts[5]),              // Flags(hex)  -> hex
                    };

                    result.Add(ev);
                }
                catch
                {
                    // Nếu cần, bạn có thể log lỗi dòng tại đây
                }
            }

            return result;
        }
        private static string VkToKeyName(uint vk)
        {
            try
            {
                var key = KeyInterop.KeyFromVirtualKey((int)vk);

                // Nếu là A-Z hoặc 0-9, hiển thị char cho dễ nhìn
                if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString(); // 0-9
                if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString(); // A-Z

                return key.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
        private bool _shiftDown = false;
        private bool _capsOn = false;
        private readonly StringBuilder _typedBuffer = new StringBuilder();


        private string? TranslateToText(uint vk, uint scan)
        {
            // Các phím điều khiển bạn muốn hiển thị dạng chữ:
            if (vk == 0x0D) return "\n";      // Enter
            if (vk == 0x09) return "\t";      // Tab
            if (vk == 0x20) return " ";       // Space
            if (vk == 0x08) return null;      // Backspace xử lý riêng

            // Keyboard layout hiện tại
            IntPtr hkl = GetKeyboardLayout(0);

            // KeyState 256 bytes
            var keyState = new byte[256];

            // Shift
            if (_shiftDown)
            {
                keyState[0x10] = 0x80; // VK_SHIFT
                keyState[0xA0] = 0x80; // VK_LSHIFT
                keyState[0xA1] = 0x80; // VK_RSHIFT
            }

            // CapsLock: bit 0 = toggle
            if (_capsOn)
                keyState[0x14] = 0x01; // VK_CAPITAL

            // Nếu scanCode trong log không chuẩn, bạn có thể map lại:
            // uint scanCode = scan != 0 ? scan : MapVirtualKeyEx(vk, 0, hkl);
            uint scanCode = scan;

            var sb = new StringBuilder(8);
            int rc = ToUnicodeEx(vk, scanCode, keyState, sb, sb.Capacity, 0, hkl);

            // rc > 0: có ký tự
            if (rc > 0)
                return sb.ToString();

            return null;
        }

        private void ApplyKeyEventToTextBuffer(KeyboardReplayEvent ev)
        {
            uint vk = ev.VkCode;

            // Cập nhật modifier
            bool isShiftKey = (vk == 0x10 || vk == 0xA0 || vk == 0xA1);
            if (isShiftKey)
            {
                if (ev.IsKeyDown) _shiftDown = true;
                if (ev.IsKeyUp) _shiftDown = false;
                return;
            }

            // CapsLock toggle trên KEYDOWN
            if (vk == 0x14 && ev.IsKeyDown)
            {
                _capsOn = !_capsOn;
                return;
            }

            // Chỉ “gõ chữ” khi KEYDOWN
            if (!ev.IsKeyDown) return;

            // Backspace
            if (vk == 0x08)
            {
                if (_typedBuffer.Length > 0) _typedBuffer.Length--;
                return;
            }

            // Dịch ra text
            var txt = TranslateToText(vk, ev.ScanCode);
            if (!string.IsNullOrEmpty(txt))
                _typedBuffer.Append(txt);
        }

        private async Task ReplayKeyboardAndShowAsync(string keyboardCsvPath, CancellationToken ct)
        {
            if (!File.Exists(keyboardCsvPath))
            {
                Dispatcher.Invoke(() => ReplayKeyboardText.Text = $"Không tìm thấy file: {keyboardCsvPath}");
                return;
            }

            var events = LoadKeyboardCsv(keyboardCsvPath, out var startTime);
            if (events.Count == 0)
            {
                Dispatcher.Invoke(() => ReplayKeyboardText.Text = "File keyboard rỗng hoặc không parse được.");
                return;
            }

            // Reset typed state
            _typedBuffer.Clear();
            _shiftDown = false;
            _capsOn = false;

            // Reset UI: chỉ hiển thị chữ
            Dispatcher.Invoke(() =>
            {
                ReplayKeyboardText.Clear();
                ReplayKeyboardText.Text = "";      // bắt đầu trống
                ReplayKeyboardText.ScrollToEnd();
            });

            uint prevTime = events[0].Time;

            for (int i = 0; i < events.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var ev = events[i];

                uint delta = (i == 0) ? 0 : (ev.Time - prevTime);
                prevTime = ev.Time;

                if (delta > 0)
                    await Task.Delay((int)Math.Min(delta, 10_000), ct);

                // ✅ Cập nhật buffer chữ theo event (KEYDOWN -> thêm ký tự, Backspace -> xoá, ...)
                ApplyKeyEventToTextBuffer(ev);

                // ✅ Update UI theo nhịp replay (KHÔNG append dòng mới)
                Dispatcher.Invoke(() =>
                {
                    ReplayKeyboardText.Text = _typedBuffer.ToString();
                    ReplayKeyboardText.CaretIndex = ReplayKeyboardText.Text.Length; // con trỏ cuối
                    ReplayKeyboardText.ScrollToEnd();
                });
            }
        }



    }
}
