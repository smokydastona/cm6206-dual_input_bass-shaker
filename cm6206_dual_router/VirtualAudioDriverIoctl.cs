using System;
using System.Runtime.InteropServices;

namespace Cm6206DualRouter;

internal static class VirtualAudioDriverIoctl
{
    // Per-endpoint device interfaces (proposed). The SysVAD fork should expose two device interfaces
    // that the router opens to pull PCM via IOCTL.
    public const string GameDeviceWin32Path = "\\\\.\\CMVADR_Game";
    public const string ShakerDeviceWin32Path = "\\\\.\\CMVADR_Shaker";

    // CTL_CODE constants
    private const uint FileDeviceUnknown = 0x00000022;
    private const uint MethodBuffered = 0;
    private const uint MethodOutDirect = 2;

    // CTL_CODE access uses FILE_*_ACCESS. FILE_READ_ACCESS == 1.
    private const uint FileReadAccess = 0x0001;

    private static uint CtlCode(uint deviceType, uint function, uint method, uint access)
        => (deviceType << 16) | (access << 14) | (function << 2) | method;

    // Function codes (exact contract)
    private const uint FunctionOpenStream = 0x801;
    private const uint FunctionRead = 0x802;
    private const uint FunctionGetFormat = 0x803;

    // IOCTLs (exact contract)
    public static readonly uint IoctlOpenStream = CtlCode(FileDeviceUnknown, FunctionOpenStream, MethodBuffered, FileReadAccess);
    public static readonly uint IoctlRead = CtlCode(FileDeviceUnknown, FunctionRead, MethodOutDirect, FileReadAccess);
    public static readonly uint IoctlGetFormat = CtlCode(FileDeviceUnknown, FunctionGetFormat, MethodBuffered, FileReadAccess);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct CmvadrAudioFormat(
        uint SampleRate,
        uint BitsPerSample,
        uint Channels);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct CmvadrReadRequest(
        uint RequestedFrames);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct CmvadrReadResponseHeader(
        uint FramesReturned);
}
