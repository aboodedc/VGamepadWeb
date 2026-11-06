using System.Runtime.InteropServices;
using VGamepadWeb.Core;
using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        /// <summary>
        ///  The main entry point of the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // WinExe 默认没有控制台，Console.WriteLine 全部被丢弃。
            // 这里接上启动它的那个控制台（dotnet run / cmd 窗口），让日志在终端里直接看得见。
            AttachParentConsole();

            ServerLog.Reset();
            ServerLog.Info("VGamepadWeb 启动");

            // 系统弹窗要在界面起来之前弹，语言得从落盘设置里先读出来。
            AppSettings settings = AppSettings.Load();
            string language = settings.Language;

            // 单实例：两个 host 会抢同一个端口、抢同一份 settings.json，托盘里还会并排两个图标。
            // 拿不到就退出（句柄活到 Main 结束，也就是进程退出）。
            using Mutex singleInstance = new Mutex(true, "VGamepadWeb.SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    Strings.WithDirectionMarks(Strings.T(language, "dialog.alreadyRunning.text"), language),
                    Strings.WithDirectionMarks(Strings.T(language, "dialog.alreadyRunning.title"), language),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ServerLog.Warn("已有实例在运行，本次启动退出");
                return;
            }

            // 🔥 فحص وجود تعريف الـ ViGEmBus وتثبيته تلقائياً إذا كان مفقوداً
            if (!DriverHelper.EnsureViGEmDriverInstalled())
            {
                // إذا فشل التثبيت أو رفض المستخدم إعطاء الصلاحيات، نغلق البرنامج لمنع الانهيار
                MessageBox.Show(
                    Strings.WithDirectionMarks(Strings.T(language, "dialog.driverMissing.text"), language),
                    Strings.WithDirectionMarks(Strings.T(language, "dialog.driverMissing.title"), language),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                ServerLog.Error("ViGEmBus 驱动缺失，程序退出");
                return;
            }

            ServerLog.Info("ViGEmBus 驱动正常");

            // إذا كان التعريف موجوداً أو تم تثبيته بنجاح، يفتح البرنامج بشكل طبيعي
            Application.Run(new Form1());
        }

        /// <summary>
        /// 挂到父进程的控制台；有就复用，没有也不新建（双击 exe 时不该凭空弹一个黑窗）。
        /// 日志同时写进 %APPDATA%\VGamepadWeb\server.log，所以没有控制台也能查。
        /// </summary>
        private static void AttachParentConsole()
        {
            try
            {
                AttachConsole(-1);
            }
            catch
            {
                // 没有父控制台：忽略。
            }
        }
    }
}
