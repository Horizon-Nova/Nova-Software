using System;
using System.Collections.Generic;

namespace NetToolTestArea.Model
{
    public class ControlInfo
    {
        public IntPtr Handle { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int CommandId { get; set; } = -1;
        public List<ControlInfo> ChildControls { get; set; }

        public override string ToString()
        {
            string cmdIdStr = CommandId >= 0 ? $"\r\nWM_COMMAND ID: {CommandId} (0x{CommandId:X})" : "";
            return $"句柄: 0x{Handle.ToInt64():X}\r\n" +
                   $"視窗標題: {WindowTitle}\r\n" +
                   $"類名: {ClassName}\r\n" +
                   $"文字內容: {Text}{cmdIdStr}";
        }
    }
}

