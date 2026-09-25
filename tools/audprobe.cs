// probe: default audio endpoint volume via IAudioEndpointVolume (the exact
// interop shape that will go into TechRainWallpaper.cs)
using System;
using System.Runtime.InteropServices;
using System.Threading;

class AudProbe
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr collection);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        int GetDevice(string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        int OpenPropertyStore(int access, out IntPtr props);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, ref Guid ctx);
        int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
        int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        int GetChannelVolumeLevel(uint ch, out float level);
        int GetChannelVolumeLevelScalar(uint ch, out float level);
        int SetMute(bool mute, ref Guid ctx);
        int GetMute(out bool mute);
        int VolumeStepUp(ref Guid ctx);
        int VolumeStepDown(ref Guid ctx);
        int QueryHardwareSupport(out uint support);
        int GetVolumeRange(out float min, out float max, out float step);
    }

    static Guid ctx = Guid.Empty;

    static void Main()
    {
        try
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice dev;
            int hr = en.GetDefaultAudioEndpoint(0, 1, out dev);   // eRender, eMultimedia
            Console.WriteLine("GetDefaultAudioEndpoint hr=0x" + hr.ToString("X"));
            string id; dev.GetId(out id);
            Console.WriteLine("device: " + id);
            object o; Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
            dev.Activate(ref iid, 23, IntPtr.Zero, out o);        // CLSCTX_ALL
            IAudioEndpointVolume vol = (IAudioEndpointVolume)o;
            float lv; vol.GetMasterVolumeLevelScalar(out lv);
            bool mu; vol.GetMute(out mu);
            float min, max, step; vol.GetVolumeRange(out min, out max, out step);
            Console.WriteLine("volume=" + (lv * 100f).ToString("F1") + "% mute=" + mu + " range=" + min + ".." + max);
            // nudge up then back, prove writes work
            float nv = Math.Min(1f, lv + 0.05f);
            vol.SetMasterVolumeLevelScalar(nv, ref ctx);
            Thread.Sleep(50);
            float chk; vol.GetMasterVolumeLevelScalar(out chk);
            Console.WriteLine("after +5% -> " + (chk * 100f).ToString("F1") + "%");
            vol.SetMasterVolumeLevelScalar(lv, ref ctx);
            Console.WriteLine("restored to " + (lv * 100f).ToString("F1") + "%");
            Console.WriteLine("AUDPROBE OK");
        }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex); }
    }
}
