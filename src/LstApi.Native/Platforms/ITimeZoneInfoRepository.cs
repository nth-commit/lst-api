using System.Runtime.InteropServices;
using LstApi.Native.Platforms.Windows;

namespace LstApi.Native.Platforms;

public interface ITimeZoneInfoRepository
{
    public Task Save(TimeZoneInfo timeZoneInfo);
    
    public static ITimeZoneInfoRepository Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsTimeZoneInfoRepository();
        }
        
        throw new PlatformNotSupportedException();
    }
}