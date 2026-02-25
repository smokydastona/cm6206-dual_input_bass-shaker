using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cm6206DualRouter;

public static class Cm6206RegisterDecoder
{
    public static string Decode(IReadOnlyDictionary<int, ushort> regs)
    {
        if (regs is null) throw new ArgumentNullException(nameof(regs));

        var sb = new StringBuilder();
        foreach (var reg in regs.Keys.OrderBy(k => k))
        {
            sb.AppendLine($"== REG{reg} ==");
            sb.AppendLine($"Raw: 0x{regs[reg]:X4} ({regs[reg]})");

            switch (reg)
            {
                case 0:
                    DecodeReg0(sb, regs[reg]);
                    break;
                case 1:
                    DecodeReg1(sb, regs[reg]);
                    break;
                case 2:
                    DecodeReg2(sb, regs[reg]);
                    break;
                case 3:
                    DecodeReg3(sb, regs[reg]);
                    break;
                case 4:
                    DecodeReg4(sb, regs[reg]);
                    break;
                case 5:
                    DecodeReg5(sb, regs[reg]);
                    break;
                default:
                    sb.AppendLine("(No decoder for this register)");
                    break;
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static void DecodeReg0(StringBuilder sb, ushort v)
    {
        sb.AppendLine($"DMA Master: {Bit(v, 15, when0: "DAC", when1: "SPDIF Out")}");
        sb.AppendLine($"SPDIF Out sample rate: {SpdifOutRate((v >> 12) & 0x7)}");
        sb.AppendLine($"Category code: {(v >> 4) & 0xFF}");
        sb.AppendLine($"Emphasis: {Bit(v, 3, when0: "None", when1: "CD_Type")}");
        sb.AppendLine($"Copyright: {Bit(v, 2, when0: "Asserted", when1: "Not Asserted")}");
        sb.AppendLine($"Non-audio: {Bit(v, 1, when0: "PCM", when1: "non-PCM (e.g. AC3)")}");
        sb.AppendLine($"Professional/Consumer: {Bit(v, 0, when0: "Consumer", when1: "Professional")}");
    }

    private static void DecodeReg1(StringBuilder sb, ushort v)
    {
        sb.AppendLine($"SEL Clk (test): {Bit(v, 14, when0: "24.576 MHz", when1: "22.58 MHz")}");
        sb.AppendLine($"PLL binary search enable: {YesNo(BitBool(v, 13))}");
        sb.AppendLine($"Soft mute enable: {YesNo(BitBool(v, 12))}");

        // GPIO1..4 pairs
        sb.AppendLine($"GPIO1 Out Enable: {YesNo(BitBool(v, 4))} | Status: {YesNo(BitBool(v, 5))}");
        sb.AppendLine($"GPIO2 Out Enable: {YesNo(BitBool(v, 6))} | Status: {YesNo(BitBool(v, 7))}");
        sb.AppendLine($"GPIO3 Out Enable: {YesNo(BitBool(v, 8))} | Status: {YesNo(BitBool(v, 9))}");
        sb.AppendLine($"GPIO4 Out Enable: {YesNo(BitBool(v, 10))} | Status: {YesNo(BitBool(v, 11))}");

        sb.AppendLine($"SPDIF Out Valid: {YesNo(BitBool(v, 3))}");
        sb.AppendLine($"SPDIF Loop-back enable: {YesNo(BitBool(v, 2))}");
        sb.AppendLine($"SPDIF Out Disable: {YesNo(BitBool(v, 1))}");
        sb.AppendLine($"SPDIF In Mix Enable: {YesNo(BitBool(v, 0))}");
    }

    private static void DecodeReg2(StringBuilder sb, ushort v)
    {
        sb.AppendLine($"Driver On: {YesNo(BitBool(v, 15))}");
        sb.AppendLine($"Headphone Source: {HeadphoneSource((v >> 13) & 0x3)}");

        sb.AppendLine($"Mute Headphone L: {YesNo(BitBool(v, 11))} | R: {YesNo(BitBool(v, 12))}");
        sb.AppendLine($"Mute Front: L={YesNo(BitBool(v, 3))} R={YesNo(BitBool(v, 4))}");
        sb.AppendLine($"Mute Center: {YesNo(BitBool(v, 5))} | Sub (LFE): {YesNo(BitBool(v, 6))}");
        sb.AppendLine($"Mute Side: L={YesNo(BitBool(v, 7))} R={YesNo(BitBool(v, 8))}");
        sb.AppendLine($"Mute Rear: L={YesNo(BitBool(v, 9))} R={YesNo(BitBool(v, 10))}");

        sb.AppendLine($"BTL mode enable: {YesNo(BitBool(v, 2))}");
        sb.AppendLine($"MCU Clock Frequency: {McuClock((v >> 0) & 0x3)}");
    }

    private static void DecodeReg3(StringBuilder sb, ushort v)
    {
        sb.AppendLine($"FLY tuner volume sensitivity: {(v >> 11) & 0x7}");
        sb.AppendLine($"Microphone bias voltage: {Bit(v, 10, when0: "4.5 V", when1: "2.25 V")}");

        // Note: some references indicate bit 9 meaning is inverted vs datasheet.
        sb.AppendLine($"Mix MIC/Line In to: {Bit(v, 9, when0: "All 8 Channels", when1: "Front Out Only")}");

        sb.AppendLine($"SPDIF In sample rate: {SpdifInRate((v >> 7) & 0x3)}");
        sb.AppendLine($"Package size: {Bit(v, 6, when0: "100 pins", when1: "48 pins")}");

        sb.AppendLine($"Front Out Enable: {YesNo(BitBool(v, 5))}");
        sb.AppendLine($"Rear Out Enable: {YesNo(BitBool(v, 4))}");
        sb.AppendLine($"Center Out Enable: {YesNo(BitBool(v, 3))}");
        sb.AppendLine($"Line Out Enable: {YesNo(BitBool(v, 2))}");
        sb.AppendLine($"Headphone Out Enable: {YesNo(BitBool(v, 1))}");
        sb.AppendLine($"SPDIF In can be recorded: {YesNo(BitBool(v, 0))}");
    }

    private static void DecodeReg4(StringBuilder sb, ushort v)
    {
        // GPIO5 has reversed bit order in some references (enable=1, status=0)
        sb.AppendLine($"GPIO12 Out Enable: {YesNo(BitBool(v, 14))} | Status: {YesNo(BitBool(v, 15))}");
        sb.AppendLine($"GPIO11 Out Enable: {YesNo(BitBool(v, 12))} | Status: {YesNo(BitBool(v, 13))}");
        sb.AppendLine($"GPIO10 Out Enable: {YesNo(BitBool(v, 10))} | Status: {YesNo(BitBool(v, 11))}");
        sb.AppendLine($"GPIO9 Out Enable: {YesNo(BitBool(v, 8))} | Status: {YesNo(BitBool(v, 9))}");
        sb.AppendLine($"GPIO8 Out Enable: {YesNo(BitBool(v, 6))} | Status: {YesNo(BitBool(v, 7))}");
        sb.AppendLine($"GPIO7 Out Enable: {YesNo(BitBool(v, 4))} | Status: {YesNo(BitBool(v, 5))}");
        sb.AppendLine($"GPIO6 Out Enable: {YesNo(BitBool(v, 2))} | Status: {YesNo(BitBool(v, 3))}");
        sb.AppendLine($"GPIO5 Out Enable: {YesNo(BitBool(v, 1))} | Status: {YesNo(BitBool(v, 0))}");
    }

    private static void DecodeReg5(StringBuilder sb, ushort v)
    {
        sb.AppendLine($"DAC Not Reset: {YesNo(BitBool(v, 13))}");
        sb.AppendLine($"ADC Not Reset: {YesNo(BitBool(v, 12))}");
        sb.AppendLine($"ADC to SPDIF Out: {YesNo(BitBool(v, 11))}");
        sb.AppendLine($"SPDIF Out select: {SpdifOutChannel((v >> 9) & 0x3)}");
        sb.AppendLine($"USB/CODEC Mode: {Bit(v, 8, when0: "CODEC", when1: "USB")}");
        sb.AppendLine($"DAC high pass filter: {YesNo(BitBool(v, 7))}");

        sb.AppendLine($"Loopback ADC → Front DAC: {YesNo(BitBool(v, 3))}");
        sb.AppendLine($"Loopback ADC → Side DAC: {YesNo(BitBool(v, 4))}");
        sb.AppendLine($"Loopback ADC → Center DAC: {YesNo(BitBool(v, 5))}");
        sb.AppendLine($"Loopback ADC → Rear DAC: {YesNo(BitBool(v, 6))}");

        sb.AppendLine($"Input source to AD digital filter: {AdFilterSource((v >> 0) & 0x7)}");
    }

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static bool BitBool(ushort v, int bit) => ((v >> bit) & 1) != 0;

    private static string Bit(ushort v, int bit, string when0, string when1) => BitBool(v, bit) ? when1 : when0;

    private static string SpdifOutRate(int value) => value switch
    {
        0 => "44.1 kHz",
        2 => "48 kHz",
        3 => "32 kHz",
        6 => "96 kHz",
        _ => $"Reserved ({value})"
    };

    private static string SpdifInRate(int value) => value switch
    {
        0 => "44.1 kHz",
        2 => "48 kHz",
        3 => "32 kHz",
        _ => $"Reserved ({value})"
    };

    private static string HeadphoneSource(int value) => value switch
    {
        0 => "Side",
        1 => "Rear",
        2 => "Center/Subwoofer",
        3 => "Front",
        _ => $"Reserved ({value})"
    };

    private static string McuClock(int value) => value switch
    {
        0 => "1.5 MHz",
        1 => "3 MHz",
        _ => $"Reserved ({value})"
    };

    private static string SpdifOutChannel(int value) => value switch
    {
        0 => "Front",
        1 => "Side",
        2 => "Center",
        3 => "Rear",
        _ => $"Reserved ({value})"
    };

    private static string AdFilterSource(int value) => value switch
    {
        0 => "Normal",
        4 => "Front",
        5 => "Side",
        6 => "Center",
        7 => "Rear",
        _ => $"Reserved ({value})"
    };
}
