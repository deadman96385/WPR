using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Phone.Info
{
    public class DeviceExtendedProperties
    {
        // The phone hands DeviceUniqueId back as a 20-byte array, and titles
        // consume it without a null check: Doodle God casts the result straight
        // to Byte[] - which null passes - and feeds it to SHA1.ComputeHash, so a
        // null here throws ArgumentNullException out of its Launching handler
        // before the handler loads its splash image.
        //
        // It has to be stable across runs. A fresh value each launch would read
        // as a new device to any title that keys saved data or a registration on
        // it, so this is derived from the host rather than generated.
        private static readonly Lazy<byte[]> UniqueId = new(BuildUniqueId);

        private static byte[] BuildUniqueId()
        {
            string seed = $"WPRunner|{Environment.MachineName}|{Environment.UserName}";
            return SHA1.HashData(Encoding.UTF8.GetBytes(seed));
        }
        public static bool TryGetValue(string propertyName, out Object propertyValue)
        {
            object? value = GetValue(propertyName);
            propertyValue = value!;
            return value != null;
        }

        public static Object? GetValue(string property)
        {
            switch (property)
            {
                case "DeviceUniqueId":
                    // A copy per call: the phone's array is the caller's to keep,
                    // and titles hash or index it in place.
                    return (byte[])UniqueId.Value.Clone();

                case "DeviceManufacturer":
                    return "WPRunner";

                case "DeviceName":
                    return "WPRunner 2022";

                case "DeviceFirmwareVersion":
                case "DeviceHardwareVersion":
                    return "8.0.0";

                case "DeviceTotalMemory":
                    return DeviceStatus.DeviceTotalMemory;

                case "ApplicationCurrentMemoryUsage":
                    return DeviceStatus.ApplicationCurrentMemoryUsage;

                case "ApplicationPeakMemoryUsage":
                    return DeviceStatus.ApplicationPeakMemoryUsage;

                case "ApplicationMemoryUsageLimit":
                case "ApplicationWorkingSetLimit":
                    return DeviceStatus.ApplicationMemoryUsageLimit;

                default:
                    return null;
            }
        }
    }
}
