using System.Runtime.InteropServices;

namespace Lst.WindowsService;

public static class StructTools
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