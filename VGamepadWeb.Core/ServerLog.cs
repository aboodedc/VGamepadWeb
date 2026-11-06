using System.Text;

namespace VGamepadWeb.Core
{
    /// <summary>
    /// 服务端日志出口：同时写控制台和 %APPDATA%\VGamepadWeb\server.log。
    ///
    /// 为什么不用 Console.WriteLine：WinForm 是 WinExe，没有控制台，
    /// Console.WriteLine 全部被丢弃，「启动服务到底发生什么」完全看不见。
    /// 写文件是兜底 —— 双击 exe 启动时也能留下痕迹。
    /// </summary>
    public static class ServerLog
    {
        private static readonly object Gate = new object();
        private static string? _filePath;

        /// <summary>日志文件路径：%APPDATA%\VGamepadWeb\server.log。</summary>
        public static string FilePath
        {
            get
            {
                if (_filePath == null)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "VGamepadWeb");

                    _filePath = Path.Combine(dir, "server.log");
                }

                return _filePath;
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                Write("ERROR", message + " :: " + ex.GetType().Name + ": " + ex.Message);
                Write("ERROR", ex.StackTrace ?? "(no stack trace)");
                return;
            }

            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            lock (Gate)
            {
                try
                {
                    Console.WriteLine(line);
                }
                catch
                {
                    // 没有控制台（WinExe 双击启动）时忽略。
                }

                try
                {
                    string? dir = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // 追加写；文件被占用也不能影响服务。
                    File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // 日志失败绝不能影响服务本身。
                }
            }
        }

        /// <summary>清空旧日志文件（启动时调用一次，方便看当次运行）。</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                try
                {
                    string? dir = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(FilePath, string.Empty, Encoding.UTF8);
                }
                catch
                {
                    // 清不动就算了。
                }
            }
        }
    }
}
