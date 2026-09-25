// probe: wired status (NetworkInformation) + WiFi enumeration/connect structs (wlanapi)
// read-only - never connects. Struct sizes printed to validate layouts before
// the same declarations go into TechRainWallpaper.cs
using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

class NetProbe
{
    // ---- wlanapi ----
    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint clientVer, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, uint flags, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out uint dataSize, ref IntPtr data, IntPtr opcodeType);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Desc;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DOT11_SSID { public uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Ssid; }

    [StructLayout(LayoutKind.Sequential)]
    struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID Ssid;
        public int BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
        public int PhyType;
        public uint PhyIndex;
        public uint SignalQuality;
        public uint RxRate;
        public uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WLAN_SECURITY_ATTRIBUTES
    {
        public int SecurityEnabled;
        public int AuthAlgo;
        public int CipherAlgo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_CONNECTION_ATTRIBUTES
    {
        public int State;
        public int ConnectionMode;
        public DOT11_SSID Ssid;
        public int BssType;
        public int SecurityEnabled;
        public int AuthAlgo;
        public int CipherAlgo;
        public uint Flags;
        public uint KeyIndex;
        public WLAN_ASSOCIATION_ATTRIBUTES Assoc;
        public WLAN_SECURITY_ATTRIBUTES Sec;
        public uint ReasonCode;
        public uint ProfileFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        public DOT11_SSID Ssid;
        public int BssType;
        public uint NumberOfBssids;
        public int Connectable;
        public uint NotConnectableReason;
        public uint NumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] PhyTypes;
        public int MorePhyTypes;
        public uint SignalQuality;
        public int SecurityEnabled;
        public int DefaultAuthAlgorithm;
        public int DefaultCipherAlgorithm;
        public uint Flags;
        public uint Reserved;
    }

    static string SsidStr(DOT11_SSID s)
    {
        if (s.Ssid == null || s.Length == 0 || s.Length > 32) return "";
        return Encoding.UTF8.GetString(s.Ssid, 0, (int)s.Length);
    }

    static void Main()
    {
        Console.WriteLine("sizeof: IFACE_INFO=" + Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO))
            + " CONN_ATTR=" + Marshal.SizeOf(typeof(WLAN_CONNECTION_ATTRIBUTES))
            + " AVAIL_NET=" + Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK)));

        // ---- wired / overall adapters ----
        Console.WriteLine("---- adapters ----");
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            string gw = "-";
            foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses) gw = g.Address.ToString();
            Console.WriteLine(ni.NetworkInterfaceType + " | " + ni.OperationalStatus + " | " + ni.Name
                + " | speed=" + (ni.Speed / 1000000.0).ToString("F0") + "Mbps | gw=" + gw);
        }

        // ---- wifi ----
        Console.WriteLine("---- wlan ----");
        IntPtr h; uint ver;
        int hr = WlanOpenHandle(2, IntPtr.Zero, out ver, out h);
        if (hr != 0) { Console.WriteLine("WlanOpenHandle failed 0x" + hr.ToString("X")); return; }
        IntPtr il;
        hr = WlanEnumInterfaces(h, IntPtr.Zero, out il);
        if (hr != 0) { Console.WriteLine("WlanEnumInterfaces failed 0x" + hr.ToString("X")); return; }
        uint n = (uint)Marshal.ReadInt32(il);
        Console.WriteLine("wlan interfaces: " + n);
        for (int i = 0; i < n; i++)
        {
            IntPtr info = new IntPtr(il.ToInt64() + 8 + i * Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO)));
            WLAN_INTERFACE_INFO ifo = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(info, typeof(WLAN_INTERFACE_INFO));
            Console.WriteLine("iface " + ifo.InterfaceGuid + " state=" + ifo.State + " desc=" + ifo.Desc);

            IntPtr data = IntPtr.Zero; uint size;
            hr = WlanQueryInterface(h, ref ifo.InterfaceGuid, 7, IntPtr.Zero, out size, ref data, IntPtr.Zero); // current_connection
            if (hr == 0 && data != IntPtr.Zero)
            {
                WLAN_CONNECTION_ATTRIBUTES cc = (WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(data, typeof(WLAN_CONNECTION_ATTRIBUTES));
                Console.WriteLine("  current: state=" + cc.State + " ssid='" + SsidStr(cc.Ssid) + "' sig=" + cc.Assoc.SignalQuality
                    + "% profile='" + cc.ProfileName + "' sec=" + cc.SecurityEnabled);
                WlanFreeMemory(data);
            }
            else Console.WriteLine("  current: hr=0x" + hr.ToString("X") + " (not connected?)");

            IntPtr al;
            hr = WlanGetAvailableNetworkList(h, ref ifo.InterfaceGuid, 2, IntPtr.Zero, out al);
            if (hr != 0) { Console.WriteLine("  avail list failed 0x" + hr.ToString("X")); continue; }
            uint cnt = (uint)Marshal.ReadInt32(al);
            Console.WriteLine("  available: " + cnt + " (entry size " + Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK)) + ")");
            long basePtr = al.ToInt64() + 8, stride = Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
            for (int k = 0; k < cnt; k++)
            {
                WLAN_AVAILABLE_NETWORK an = (WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(new IntPtr(basePtr + k * stride), typeof(WLAN_AVAILABLE_NETWORK));
                string ssid = SsidStr(an.Ssid);
                if (ssid.Length == 0) ssid = "<hidden>";
                Console.WriteLine("   - '" + ssid + "' sig=" + an.SignalQuality + "% sec=" + an.SecurityEnabled
                    + " auth=" + an.DefaultAuthAlgorithm + " cipher=" + an.DefaultCipherAlgorithm
                    + " flags=" + an.Flags + " profile='" + an.ProfileName + "' connectable=" + an.Connectable);
            }
            WlanFreeMemory(al);
        }
        WlanCloseHandle(h, IntPtr.Zero);
        Console.WriteLine("NETPROBE OK");
    }
}
