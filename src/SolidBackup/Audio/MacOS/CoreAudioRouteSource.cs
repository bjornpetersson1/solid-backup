using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

using SolidBackup.Audio.Devices;

namespace SolidBackup.Audio.MacOS;

/// <summary>
/// Reads an output device's transport type and current data source from CoreAudio, finding the
/// device by the same name PortAudio reports. The devices are read anew on every call, so a headphone plugged in or
/// out since the last call is seen (monitoring depends on it).
/// </summary>
public sealed class CoreAudioRouteSource : IOutputRouteSource
{
    private const string CoreAudioPath = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
    private const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint SystemObject = 1;
    private const uint MainElement = 0;

    private static readonly uint Devices = FourCharCode("dev#");
    private static readonly uint Name = FourCharCode("lnam");
    private static readonly uint TransportType = FourCharCode("tran");
    private static readonly uint DataSource = FourCharCode("ssrc");
    private static readonly uint GlobalScope = FourCharCode("glob");
    private static readonly uint OutputScope = FourCharCode("outp");

    public OutputRoute? Route(string deviceName)
    {
        return ReadRoutes().GetValueOrDefault(deviceName);
    }

    private static Dictionary<string, OutputRoute> ReadRoutes()
    {
        Dictionary<string, OutputRoute> routes = [];
        if (!OperatingSystem.IsMacOS())
        {
            return routes;
        }

        try
        {
            foreach (uint device in DeviceIds())
            {
                if (DeviceName(device) is { } name && ReadUInt32(device, TransportType, GlobalScope) is { } transport)
                {
                    routes.TryAdd(name, new OutputRoute(FourCharString(transport), BuiltInDataSource(device, transport)));
                }
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            routes.Clear();
        }

        return routes;
    }

    // Only the speaker check reads the data source, and only for built-in devices.
    private static string? BuiltInDataSource(uint device, uint transport)
    {
        return FourCharString(transport) == OutputRoute.BuiltInTransport && ReadUInt32(device, DataSource, OutputScope) is { } code
            ? FourCharString(code)
            : null;
    }

    private static uint[] DeviceIds()
    {
        PropertyAddress address = new(Devices, GlobalScope, MainElement);
        if (AudioObjectGetPropertyDataSize(SystemObject, ref address, 0, IntPtr.Zero, out uint size) != 0)
        {
            return [];
        }

        uint[] devices = new uint[size / sizeof(uint)];
        return AudioObjectGetPropertyData(SystemObject, ref address, 0, IntPtr.Zero, ref size, devices) == 0 ? devices : [];
    }

    private static string? DeviceName(uint device)
    {
        PropertyAddress address = new(Name, GlobalScope, MainElement);
        uint size = (uint)IntPtr.Size;
        if (AudioObjectGetPropertyData(device, ref address, 0, IntPtr.Zero, ref size, out IntPtr name) != 0 || name == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            char[] characters = new char[CFStringGetLength(name)];
            CFStringGetCharacters(name, new CFRange(0, characters.Length), characters);
            return new string(characters);
        }
        finally
        {
            CFRelease(name);
        }
    }

    private static uint? ReadUInt32(uint device, uint selector, uint scope)
    {
        PropertyAddress address = new(selector, scope, MainElement);
        uint size = sizeof(uint);
        return AudioObjectGetPropertyData(device, ref address, 0, IntPtr.Zero, ref size, out uint value) == 0 ? value : null;
    }

    private static uint FourCharCode(string code)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(code));
    }

    private static string FourCharString(uint code)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, code);
        return Encoding.ASCII.GetString(bytes);
    }

    [DllImport(CoreAudioPath)]
    private static extern int AudioObjectGetPropertyDataSize(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, out uint size);

    [DllImport(CoreAudioPath)]
    private static extern int AudioObjectGetPropertyData(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, [Out] uint[] data);

    [DllImport(CoreAudioPath)]
    private static extern int AudioObjectGetPropertyData(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, out uint data);

    [DllImport(CoreAudioPath)]
    private static extern int AudioObjectGetPropertyData(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, out IntPtr data);

    [DllImport(CoreFoundationPath)]
    private static extern nint CFStringGetLength(IntPtr text);

    [DllImport(CoreFoundationPath, CharSet = CharSet.Unicode)]
    private static extern void CFStringGetCharacters(IntPtr text, CFRange range, [Out] char[] buffer);

    [DllImport(CoreFoundationPath)]
    private static extern void CFRelease(IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct PropertyAddress(uint Selector, uint Scope, uint Element);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CFRange(nint Location, nint Length);
}
