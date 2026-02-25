using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;

namespace Cm6206DualRouter;

public static class Cm6206HidClient
{
    // Common VID/PID for CM6206 based adapters (as used by cm6206ctl).
    public const int VendorId = 0x0D8C;
    public const int ProductId = 0x0102;

    // This device is controlled via small HID reports.
    // We keep this read-only by default.
    private const byte OpWriteRegister = 0x20;
    private const byte OpReadRegister = 0x30;

    public sealed record HidDeviceInfo(
        string DevicePath,
        string? Manufacturer,
        string? Product,
        string? SerialNumber);

    public static IReadOnlyList<HidDeviceInfo> EnumerateDevices()
    {
        var list = DeviceList.Local;
        return list.GetHidDevices(VendorId, ProductId)
            .Select(d => new HidDeviceInfo(d.DevicePath, d.Manufacturer, d.ProductName, d.SerialNumber))
            .ToList();
    }

    public static IReadOnlyDictionary<int, ushort> ReadRegisterBlock(string devicePath, int registerCount = 6)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
            throw new ArgumentException("devicePath is required", nameof(devicePath));

        if (registerCount <= 0 || registerCount > 64)
            throw new ArgumentOutOfRangeException(nameof(registerCount), "registerCount must be in 1..64");

        var device = DeviceList.Local.GetHidDevices(VendorId, ProductId)
            .FirstOrDefault(d => string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));

        if (device is null)
            throw new InvalidOperationException("Selected HID device is no longer available.");

        if (!device.TryOpen(out var stream))
            throw new InvalidOperationException("Failed to open HID interface (access denied or device busy). Try unplug/replug or run as admin.");

        using (stream)
        {
            stream.ReadTimeout = 500;
            stream.WriteTimeout = 500;

            var results = new Dictionary<int, ushort>(registerCount);
            for (var reg = 0; reg < registerCount; reg++)
            {
                results[reg] = ReadRegister(stream, device, reg);
            }
            return results;
        }
    }

    private static ushort ReadRegister(HidStream stream, HidDevice device, int reg)
    {
        if (reg < 0 || reg > 255) throw new ArgumentOutOfRangeException(nameof(reg));

        var outLen = Math.Max(device.GetMaxOutputReportLength(), 5);
        var inLen = Math.Max(device.GetMaxInputReportLength(), 3);

        var outBuf = new byte[outLen];
        // Report ID (0 if none)
        outBuf[0] = 0x00;
        outBuf[1] = OpReadRegister;
        outBuf[2] = 0x00; // data low (unused)
        outBuf[3] = 0x00; // data high (unused)
        outBuf[4] = (byte)reg;

        stream.Write(outBuf, 0, outBuf.Length);

        var inBuf = new byte[inLen];
        var read = stream.Read(inBuf, 0, inBuf.Length);
        if (read < 3)
            throw new InvalidOperationException($"Short HID read ({read} bytes). Device did not return register data.");

        // Response format we expect:
        //   byte0: status/opcode with upper bits 0x20 when register data is present
        //   byte1: DATAL
        //   byte2: DATAH
        var status = inBuf[0];
        if ((status & 0xE0) != OpWriteRegister)
            throw new InvalidOperationException($"Unexpected HID response 0x{status:X2} while reading reg {reg}.");

        var value = (ushort)((inBuf[2] << 8) | inBuf[1]);
        return value;
    }
}
