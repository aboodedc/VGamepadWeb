using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VGamepadWeb.WinForm
{
    /// <summary>
    /// 一个本机 IPv4 地址及其网卡类型。Kind 是稳定键（"wifi" / "ethernet" / "other"），
    /// 显示文案由界面按语言自己映射（Strings.IpLabel），这里不塞任何中文字面量。
    /// </summary>
    public sealed record LocalAddress(string Ip, string Kind, string Label);

    public static class NetworkHelper
    {
        /// <summary>
        /// 1. جلب جميع عناوين الـ IPv4 الشغالة في الجهاز مرتبة تلقائياً (الـ Wi-Fi في الصدارة)،
        /// 附带网卡类型（Wireless80211 → Wi-Fi，Ethernet → 以太网，其余 → 其它）。
        /// </summary>
        public static List<LocalAddress> GetAllLocalIPv4Addresses()
        {
            List<LocalAddress> addresses = new List<LocalAddress>();

            // جلب كروت الشبكة النشطة وترتيبها بحيث يكون الـ Wireless أولاً
            var sortedInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .OrderByDescending(item =>
                    item.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                    item.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains("802.11", StringComparison.OrdinalIgnoreCase)
                );

            foreach (NetworkInterface item in sortedInterfaces)
            {
                (string kind, string label) = MapInterfaceKind(item.NetworkInterfaceType);
                IPInterfaceProperties adapterProperties = item.GetIPProperties();

                foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                {
                    // تصفية العناوين لجلب IPv4 الحقيقية وتجاهل الـ Localhost (127.0.0.1)
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                    {
                        string ipAddress = ip.Address.ToString();

                        if (!addresses.Any(existing => existing.Ip == ipAddress))
                        {
                            addresses.Add(new LocalAddress(ipAddress, kind, label));
                        }
                    }
                }
            }

            return addresses;
        }

        /// <summary>
        /// 2. جلب عنوان الـ IPv4 المرجح والأكثر ملاءمة فقط (الـ Wi-Fi أولاً، ثم بقية الكروت، أو 127.0.0.1 كاحتياطي)
        /// </summary>
        public static string GetPreferredLocalIPv4Address()
        {
            List<LocalAddress> allIps = GetAllLocalIPv4Addresses();

            if (allIps.Count > 0)
            {
                return allIps[0].Ip; // إرجاع العنصر الأول (وهو الـ Wi-Fi المرجح بفضل الترتيب)
            }

            // خيار احتياطي آمن في حال عدم وجود أي اتصال شبكة فعال في الجهاز
            return "127.0.0.1";
        }

        private static (string kind, string label) MapInterfaceKind(NetworkInterfaceType interfaceType) => interfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => ("wifi", "Wi-Fi"),
            NetworkInterfaceType.Ethernet => ("ethernet", "以太网"),
            _ => ("other", "其它")
        };
    }
}
