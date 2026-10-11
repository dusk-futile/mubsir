using System.Runtime.InteropServices;

namespace Basira.App.Actions.Interop;

// Core Audio (MMDevice API) through built-in COM interop. Only the vtable slots up to the last method used are
// declared, in their exact order; the rest of each interface is never called.

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(nint notify);

    [PreserveSig]
    int UnregisterControlChangeNotify(nint notify);

    [PreserveSig]
    int GetChannelCount(out uint count);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDb, ref Guid context);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, ref Guid context);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDb);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);

    [PreserveSig]
    int GetChannelVolumeLevel(uint channel, out float levelDb);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint channel, out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

/// <summary>Default playback device volume (level 0–100 and mute). Every call is HRESULT-checked; failures return null/false.</summary>
internal static class SystemVolume
{
    private const int ERender = 0;
    private const int EMultimedia = 1;
    private const int ClsctxAll = 0x17;

    public static (int Level, bool Muted)? Get()
    {
        return With(v =>
        {
            if (v.GetMasterVolumeLevelScalar(out float level) < 0 || v.GetMute(out bool muted) < 0)
            {
                return ((int Level, bool Muted)?)null;
            }

            return ((int)Math.Round(level * 100), muted);
        });
    }

    public static bool Set(int? level, bool? mute)
    {
        return With(v =>
        {
            var context = Guid.Empty;
            bool ok = true;
            if (level is { } l)
            {
                ok &= v.SetMasterVolumeLevelScalar(Math.Clamp(l, 0, 100) / 100f, ref context) >= 0;
            }

            if (mute is { } m)
            {
                ok &= v.SetMute(m, ref context) >= 0;
            }

            return ok;
        });
    }

    private static T? With<T>(Func<IAudioEndpointVolume, T> work)
    {
        object? enumeratorObj = null;
        IMMDevice? device = null;
        object? volumeObj = null;
        try
        {
            enumeratorObj = new MMDeviceEnumeratorComObject();
            var enumerator = (IMMDeviceEnumerator)enumeratorObj;
            if (enumerator.GetDefaultAudioEndpoint(ERender, EMultimedia, out device) < 0 || device is null)
            {
                return default;
            }

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, ClsctxAll, 0, out volumeObj) < 0 || volumeObj is not IAudioEndpointVolume volume)
            {
                return default;
            }

            return work(volume);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or PlatformNotSupportedException)
        {
            return default;
        }
        finally
        {
            Release(volumeObj);
            Release(device);
            Release(enumeratorObj);
        }
    }

    private static void Release(object? o)
    {
        if (o is not null && Marshal.IsComObject(o))
        {
            Marshal.ReleaseComObject(o);
        }
    }
}
