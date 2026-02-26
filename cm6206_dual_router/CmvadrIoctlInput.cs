using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NAudio.Wave;

namespace Cm6206DualRouter;

internal sealed class CmvadrIoctlInput : IDisposable
{
    private readonly string _devicePath;
    private readonly BufferedWaveProvider _buffer;
    private readonly SafeFileHandle _handle;

    private CancellationTokenSource? _cts;
    private Task? _task;

    public WaveFormat WaveFormat { get; }

    public BufferedWaveProvider Buffer => _buffer;

    private readonly int _bytesPerFrame;

    private CmvadrIoctlInput(string devicePath, SafeFileHandle handle, VirtualAudioDriverIoctl.CmvadrAudioFormat fmt)
    {
        _devicePath = devicePath;
        _handle = handle;

        WaveFormat = CreateWaveFormat(fmt);
        _bytesPerFrame = checked((int)(fmt.Channels * (fmt.BitsPerSample / 8)));

        _buffer = new BufferedWaveProvider(WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };
    }

    public static CmvadrIoctlInput Open(string devicePath)
    {
        var handle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateFile failed for {devicePath}");

        // OPEN_STREAM
        NativeMethods.DeviceIoControlNoOutput(handle, VirtualAudioDriverIoctl.IoctlOpenStream);

        // GET_FORMAT
        var format = NativeMethods.DeviceIoControlGetStruct<VirtualAudioDriverIoctl.CmvadrAudioFormat>(handle, VirtualAudioDriverIoctl.IoctlGetFormat);

        if (format.SampleRate == 0 || format.Channels == 0 || format.BitsPerSample == 0)
            throw new InvalidOperationException($"Driver returned an invalid format for {devicePath}: {format}");

        return new CmvadrIoctlInput(devicePath, handle, format);
    }

    public static VirtualAudioDriverIoctl.CmvadrAudioFormat ProbeFormat(string devicePath)
    {
        using var handle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateFile failed for {devicePath}");

        NativeMethods.DeviceIoControlNoOutput(handle, VirtualAudioDriverIoctl.IoctlOpenStream);
        return NativeMethods.DeviceIoControlGetStruct<VirtualAudioDriverIoctl.CmvadrAudioFormat>(handle, VirtualAudioDriverIoctl.IoctlGetFormat);
    }

    public void Start()
    {
        if (_cts is not null)
            return;

        _cts = new CancellationTokenSource();
        _task = Task.Run(() => ReadLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();

        try { _task?.Wait(500); } catch { /* ignore */ }

        _task = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Stop();
        _handle.Dispose();
    }

    private void ReadLoop(CancellationToken ct)
    {
        // Keep reads small to reduce latency; the driver can block until data is available.
        const uint requestedFrames = 256;

        var request = new VirtualAudioDriverIoctl.CmvadrReadRequest(requestedFrames);

        // Allocate enough space for the response header + payload.
        // Note: for METHOD_OUT_DIRECT, some driver implementations may return raw PCM only.
        var headerSize = Marshal.SizeOf<VirtualAudioDriverIoctl.CmvadrReadResponseHeader>();
        var maxPayloadBytes = checked((int)requestedFrames * _bytesPerFrame);
        var outBuffer = new byte[checked(headerSize + maxPayloadBytes)];

        while (!ct.IsCancellationRequested)
        {
            int bytesReturned;
            try
            {
                bytesReturned = NativeMethods.DeviceIoControlRead(_handle, VirtualAudioDriverIoctl.IoctlRead, request, outBuffer);
            }
            catch
            {
                // If the driver goes away, exit the loop and let the router stop.
                return;
            }

            if (bytesReturned <= 0)
                continue;

            var offset = 0;
            var payloadBytes = bytesReturned;

            // Tolerate either:
            // - CMVADR_READ_RESPONSE { FramesReturned + Data... }
            // - raw PCM payload (frames inferred from bytesReturned)
            if (bytesReturned >= headerSize)
            {
                var framesReturned = BitConverter.ToUInt32(outBuffer, 0);
                var expectedPayload = (long)framesReturned * _bytesPerFrame;
                if (framesReturned > 0 && framesReturned <= requestedFrames && expectedPayload <= bytesReturned - headerSize)
                {
                    offset = headerSize;
                    payloadBytes = (int)expectedPayload;
                }
            }

            if (payloadBytes > 0)
                _buffer.AddSamples(outBuffer, offset, payloadBytes);
        }
    }

    private static WaveFormat CreateWaveFormat(VirtualAudioDriverIoctl.CmvadrAudioFormat fmt)
    {
        var sr = checked((int)fmt.SampleRate);
        var ch = checked((int)fmt.Channels);
        var bps = checked((int)fmt.BitsPerSample);

        return bps switch
        {
            32 => WaveFormat.CreateIeeeFloatWaveFormat(sr, ch),
            16 => new WaveFormat(sr, bps, ch),
            24 => new WaveFormat(sr, bps, ch),
            _ => new WaveFormat(sr, bps, ch)
        };
    }

    private static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;

        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;

        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            byte[] lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        public static void DeviceIoControlNoOutput(SafeFileHandle h, uint ioctl)
        {
            var ok = DeviceIoControl(h, ioctl, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"DeviceIoControl failed (0x{ioctl:x})");
        }

        public static T DeviceIoControlGetStruct<T>(SafeFileHandle h, uint ioctl) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var outBytes = new byte[size];
            var ok = DeviceIoControl(h, ioctl, IntPtr.Zero, 0, outBytes, outBytes.Length, out var bytesReturned, IntPtr.Zero);
            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"DeviceIoControl failed (0x{ioctl:x})");
            if (bytesReturned < size)
                throw new InvalidOperationException($"DeviceIoControl returned too few bytes (0x{ioctl:x}): {bytesReturned} < {size}");

            return MemoryMarshal.Read<T>(outBytes);
        }

        public static int DeviceIoControlRead<TIn>(SafeFileHandle h, uint ioctl, TIn input, byte[] outBuffer) where TIn : struct
        {
            var inSize = Marshal.SizeOf<TIn>();
            var inPtr = Marshal.AllocHGlobal(inSize);
            try
            {
                Marshal.StructureToPtr(input, inPtr, false);

                var ok = DeviceIoControl(h, ioctl, inPtr, inSize, outBuffer, outBuffer.Length, out var bytesReturned, IntPtr.Zero);
                if (!ok)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"DeviceIoControl failed (0x{ioctl:x})");

                return bytesReturned;
            }
            finally
            {
                Marshal.FreeHGlobal(inPtr);
            }
        }
    }
}
