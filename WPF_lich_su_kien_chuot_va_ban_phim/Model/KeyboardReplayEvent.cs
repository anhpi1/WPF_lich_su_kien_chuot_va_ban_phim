using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_lich_su_kien_chuot_va_ban_phim.Model
{
    internal class KeyboardReplayEvent
    {
        public uint EventIndex { get; set; }
        public uint MsgId { get; set; }      // đọc từ csv (decimal hoặc hex)
        public uint Time { get; set; }       // tick
        public uint VkCode { get; set; }     // hex -> uint
        public uint ScanCode { get; set; }   // hex -> uint
        public uint Flags { get; set; }      // hex -> uint

        public bool IsKeyDown => MsgId == 0x100 || MsgId == 0x104; // WM_KEYDOWN / WM_SYSKEYDOWN
        public bool IsKeyUp => MsgId == 0x101 || MsgId == 0x105; // WM_KEYUP   / WM_SYSKEYUP
    }
}
