using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lst.Native.Platforms.Windows;

#pragma warning disable CA1416

public class WindowsTimeZoneInfoRepository : ITimeZoneInfoRepository
{
  private const string TimeZonesRegistryHive1 = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones";

  public WindowsTimeZoneInfoRepository()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) is false)
    {
      throw new PlatformNotSupportedException();
    }
  }

  public async Task Save(TimeZoneInfo timeZoneInfo)
  {
    var model = ToModel(timeZoneInfo);

    using var tzKey = RecreateKey(TimeZonesRegistryHive1 + @"\" + timeZoneInfo.Id);
    tzKey.SetValue("Display", model.Display, RegistryValueKind.String);
    tzKey.SetValue("Dlt", model.Dlt, RegistryValueKind.String);
    tzKey.SetValue("Std", model.Std, RegistryValueKind.String);
    tzKey.SetValue("MUI_Display", model.Display, RegistryValueKind.String);
    tzKey.SetValue("MUI_Dlt", model.Dlt, RegistryValueKind.String);
    tzKey.SetValue("MUI_Std", model.Std, RegistryValueKind.String);
    tzKey.SetValue("TZI", StructTools.RawSerialize(model.Tzi), RegistryValueKind.Binary);
    tzKey.Flush();

    await Task.CompletedTask;
  }

  private static Model.TIME_ZONE ToModel(TimeZoneInfo timeZoneInfo)
  {
    // The registry can only store one timezone adjustment per year 🥲. Just take the first adjustment and save it
    // as the "base UTC offset". This'll need to run in a service if we want to keep it up-to-date.

    var firstAdjustment = timeZoneInfo.GetAdjustmentRules().First();
    var offsetMinutes = (int)firstAdjustment.DaylightDelta.TotalMinutes;

    return new Model.TIME_ZONE(
        timeZoneInfo.DisplayName,
        timeZoneInfo.DaylightName,
        timeZoneInfo.StandardName,
        new Model.REG_TZI_FORMAT(
            -offsetMinutes, // Negate, for some reason 🤷 
            0,
            0,
            Model.SYSTEM_TIME.Empty,
            Model.SYSTEM_TIME.Empty));
  }

  private static RegistryKey RecreateKey(string name)
  {
    Registry.LocalMachine.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
    return Registry.LocalMachine.CreateSubKey(name, writable: true);
  }

  private static class StructTools
  {
    /// <summary>
    /// converts byte[] to struct
    /// </summary>
    public static T RawDeserialize<T>(byte[] rawData, int position)
    {
      var rawSize = Marshal.SizeOf(typeof(T));
      if (rawSize > rawData.Length - position)
        throw new ArgumentException("Not enough data to fill struct. Array length from position: " +
                                    (rawData.Length - position) + ", Struct length: " + rawSize);
      var buffer = Marshal.AllocHGlobal(rawSize);
      Marshal.Copy(rawData, position, buffer, rawSize);
      var retObj = (T)Marshal.PtrToStructure(buffer, typeof(T))!;
      Marshal.FreeHGlobal(buffer);
      return retObj!;
    }

    /// <summary>
    /// converts a struct to byte[]
    /// </summary>
    public static byte[] RawSerialize(object value)
    {
      var rawSize = Marshal.SizeOf(value);
      var buffer = Marshal.AllocHGlobal(rawSize);
      Marshal.StructureToPtr(value, buffer, false);
      var rawData = new byte[rawSize];
      Marshal.Copy(buffer, rawData, 0, rawSize);
      Marshal.FreeHGlobal(buffer);
      return rawData;
    }
  }

  private static class Model
  {
    public record struct TIME_ZONE(
        string Display,
        string Dlt,
        string Std,
        REG_TZI_FORMAT Tzi);

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
}

#pragma warning restore CA1416