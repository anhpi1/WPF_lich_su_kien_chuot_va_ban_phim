using System;
using System;
using System.Collections.Generic;
using System.Diagnostics; // Dùng cho Process
using System.IO;          // Dùng cho Path, File
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks; // Dùng nếu muốn kết nối bất đồng bộ (Optional)
using System.Windows;

namespace WPF_lich_su_kien_chuot_va_ban_phim.Model
{
    public class control_server_class
    {
        public NamedPipeClientStream _pipeClient;
        
        public void RunServer()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string serverPath = Path.Combine(exeDir, "server", "main.exe");

                if (!File.Exists(serverPath))
                {
                    MessageBox.Show("Cannot find server.exe at: " + serverPath, "Error");
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = serverPath;
                startInfo.WorkingDirectory = Path.GetDirectoryName(serverPath);

                // --- CẤU HÌNH ĐỂ CHẠY NGẦM ---

                // 1. Phải đặt là false để cho phép thuộc tính CreateNoWindow hoạt động
                startInfo.UseShellExecute = false;

                // 2. Thuộc tính quan trọng nhất để không hiện cửa sổ đen
                //startInfo.CreateNoWindow = true;

                // 3. Đặt trạng thái cửa sổ là ẩn (dự phòng)
                //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                // -----------------------------

                Process.Start(startInfo);

                // Debug: Bỏ comment dòng dưới để biết chắc chắn nó đã chạy ngầm
                // MessageBox.Show("Server đã khởi động ngầm!"); 
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting server: " + ex.Message);
            }
        }
        public void ConnectToPipeServer()
        {
            try
            {

                // Tên Pipe phải khớp với Server C: "\\\\.\\pipe\\MyPipe" -> C# chỉ cần "MyPipe"
                // "." nghĩa là Server nằm trên máy cục bộ (Localhost)
                _pipeClient = new NamedPipeClientStream(".", "MyPipe", PipeDirection.InOut);

                // Thông báo đang kết nối (giống printf trong C)
                // Console.WriteLine("Dang ket noi den Server..."); 

                // 2. Thực hiện kết nối
                // Hàm này sẽ đợi (block) cho đến khi Server sẵn sàng. 
                // Bạn có thể truyền số mili-giây vào Connect(timeout) để tránh treo ứng dụng nếu Server không bật.
                _pipeClient.Connect(5000); // Thử kết nối trong 5 giây (5000ms)

                // Nếu dòng này chạy, tức là hPipe != INVALID_HANDLE_VALUE
                MessageBox.Show("Đã kết nối thành công đến Server!");

                // Lúc này kết nối đã mở, nhưng chưa gửi lệnh gì cả theo yêu cầu của bạn.
                
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Không thể kết nối: Hết thời gian chờ (Server chưa bật?).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message);
            }
        }
        // Hàm gửi một chuỗi lệnh bất kỳ sang Server
        public  void SendCommand(string command)
        {
            try
            {
                // 1. Kiểm tra kết nối trước
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    MessageBox.Show("Chưa kết nối đến Server! Vui lòng nhấn Connect trước.", "Cảnh báo");
                    return;
                }
                MessageBox.Show($"Command send: {command}");

                StreamWriter writer = new StreamWriter(_pipeClient);
                writer.AutoFlush = true;
                writer.Write(command);

        


            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi gửi lệnh: " + ex.Message);
            }
        }

        
    }
}

