using System.Runtime.InteropServices;

namespace Lst.WindowsService;

public class Structs
{
    [StructLayout(LayoutKind.Sequential)]
    public record struct REG_TZI_FORMAT(
        int Bias,
        int StandardBias,
        int DaylightBias,
        SYSTEM_TIME StandardDate,
        SYSTEM_TIME DaylightDate
    );

    public record struct SYSTEM_TIME(
        ushort Year,
        ushort Month,
        ushort DayOfWeek,
        ushort Day,
        ushort Hour,
        ushort Minute,
        ushort Second,
        ushort Milliseconds
    )
    {
        public static SYSTEM_TIME Empty => new(0, 0, 0, 0, 0, 0, 0, 0);
    }
}