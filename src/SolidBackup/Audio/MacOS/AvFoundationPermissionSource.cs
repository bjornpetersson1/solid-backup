using System.Runtime.InteropServices;

using SolidBackup.Audio.Devices;

namespace SolidBackup.Audio.MacOS;

/// <summary>
/// Reads the microphone permission from AVFoundation (<c>[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio]</c>)
/// through the Objective-C runtime. macOS answers for the terminal app that runs the process.
/// </summary>
public sealed class AvFoundationPermissionSource : IMicrophonePermissionSource
{
    private const string AvFoundationPath = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";
    private const string ObjectiveCPath = "/usr/lib/libobjc.dylib";

    public MicrophonePermission Status()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return MicrophonePermission.Unknown;
        }

        try
        {
            IntPtr avFoundation = NativeLibrary.Load(AvFoundationPath);
            IntPtr audioMediaType = Marshal.ReadIntPtr(NativeLibrary.GetExport(avFoundation, "AVMediaTypeAudio"));
            IntPtr captureDevice = objc_getClass("AVCaptureDevice");
            MicrophonePermission status = (MicrophonePermission)(int)objc_msgSend(
                captureDevice, sel_registerName("authorizationStatusForMediaType:"), audioMediaType);
            return Enum.IsDefined(status) ? status : MicrophonePermission.Unknown;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return MicrophonePermission.Unknown;
        }
    }

    [DllImport(ObjectiveCPath)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjectiveCPath)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCPath)]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr argument);
}
