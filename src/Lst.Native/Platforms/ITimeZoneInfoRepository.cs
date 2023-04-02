using System.Runtime.InteropServices;
using Lst.Native.Platforms.Windows;

namespace Lst.Native.Platforms;

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