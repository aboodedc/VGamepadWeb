using Microsoft.Win32;
using System.Diagnostics;

namespace VGamepadWeb.WinForm
{
    public static class DriverHelper
    {
        /// <summary>
        /// الفحص المركزي: يتحقق من وجود التعريف، وإذا لم يجده يقوم بتثبيته فوراً
        /// </summary>
        public static bool EnsureViGEmDriverInstalled()
        {
            if (IsDriverInstalled())
            {
                Console.WriteLine("[Driver] ViGEmBus Driver is already installed.");
                return true;
            }

            Console.WriteLine("[Driver] ViGEmBus Driver NOT found! Starting automatic installation...");
            return InstallDriverSilent();
        }

        /// <summary>
        /// التحقق من سجل النظام (Registry) لمعرفة هل التعريف مثبت أم لا
        /// </summary>
        private static bool IsDriverInstalled()
        {
            // المسار الافتراضي لتعريفات الأجهزة في الويندوز ريجستري
            string registryPath = @"SYSTEM\CurrentControlSet\Services\ViGEmBus";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                return key != null; // إذا كان المفتاح موجوداً، فالتعريف مثبت
            }
        }

        /// <summary>
        /// استخراج ملف الـ MSI من موارد البرنامج وتثبيته في الخلفية بشكل صامت
        /// </summary>
        private static bool InstallDriverSilent()
        {
            // 1. تغيير امتداد الملف المؤقت إلى exe
            string tempExePath = Path.Combine(Path.GetTempPath(), "ViGEmBus_Setup.exe");

            try
            {
                var assembly = typeof(DriverHelper).Assembly;
                // 2. تحديث الاسم ليتطابق تماماً مع الملف الظاهر في الـ Solution Explorer
                string resourceName = "VGamepadWeb.WinForm.ViGEmBus_1.22.0_x64_x86_arm64.exe";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) throw new Exception("Embedded Driver resource not found.");
                    using (FileStream fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                // 3. تعديل أوامر التشغيل لتناسب ملف الـ EXE (ملفات الـ EXE الصامتة لـ ViGEm تستخدم الفلاج /quiet)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempExePath,
                    Arguments = "/quiet /norestart", // الأوامر الصامتة لملف الـ EXE الخاص بهم
                    Verb = "runas",
                    CreateNoWindow = true,
                    UseShellExecute = true
                };

                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit();

                    if (File.Exists(tempExePath)) File.Delete(tempExePath);

                    return IsDriverInstalled();
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"فشل التثبيت التلقائي للتعريف المساعد: {ex.Message}", "خطأ في النظام");
                if (File.Exists(tempExePath)) File.Delete(tempExePath);
                return false;
            }
        }
    }
}