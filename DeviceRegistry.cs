using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace OMirror
{
    internal enum DeviceAdbState
    {
        Disconnected,
        Online,
        Unauthorized,
        Offline
    }

    [DataContract]
    internal sealed class SavedDeviceRecord
    {
        [DataMember(Order = 1)]
        public string Serial;

        [DataMember(Order = 2)]
        public string Name;

        [DataMember(Order = 3)]
        public string Model;

        [DataMember(Order = 4)]
        public string AndroidVersion;

        [DataMember(Order = 5)]
        public int WindowX;

        [DataMember(Order = 6)]
        public int WindowY;
    }

    [DataContract]
    internal sealed class DeviceStoreDocument
    {
        [DataMember(Order = 1)]
        public int Version = 1;

        [DataMember(Order = 2)]
        public List<SavedDeviceRecord> Devices = new List<SavedDeviceRecord>();
    }

    internal sealed class DiscoveredDevice
    {
        public string Serial;
        public DeviceAdbState State;
        public string Name;
        public string Model;
        public string AndroidVersion;
    }

    internal sealed class DeviceDiscoveryResult
    {
        public bool Success;
        public string Error;
        public readonly List<DiscoveredDevice> Devices = new List<DiscoveredDevice>();
    }

    internal static class DeviceRegistry
    {
        private const int MaximumSavedDevices = 100;

        public static List<SavedDeviceRecord> Load(string path)
        {
            List<SavedDeviceRecord> result = new List<SavedDeviceRecord>();
            if (!File.Exists(path))
                return result;

            using (FileStream stream = File.OpenRead(path))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(DeviceStoreDocument));
                DeviceStoreDocument document = serializer.ReadObject(stream) as DeviceStoreDocument;
                if (document == null || document.Devices == null)
                    return result;

                HashSet<string> serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SavedDeviceRecord record in document.Devices)
                {
                    if (record == null)
                        continue;
                    record.Serial = Clean(record.Serial, 200);
                    if (record.Serial.Length == 0 || !serials.Add(record.Serial))
                        continue;
                    record.Name = Clean(record.Name, 80);
                    record.Model = Clean(record.Model, 160);
                    record.AndroidVersion = Clean(record.AndroidVersion, 40);
                    if (record.Name.Length == 0)
                        record.Name = "Android 设备";
                    if (record.Model.Length == 0)
                        record.Model = "已保存设备";
                    if (record.WindowX < -32000 || record.WindowX > 32000)
                        record.WindowX = 80;
                    if (record.WindowY < -32000 || record.WindowY > 32000)
                        record.WindowY = 80;
                    result.Add(record);
                    if (result.Count == MaximumSavedDevices)
                        break;
                }
            }
            return result;
        }

        public static void Save(string path, IEnumerable<SavedDeviceRecord> records)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(
                directory,
                ".devices-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                DeviceStoreDocument document = new DeviceStoreDocument();
                foreach (SavedDeviceRecord record in records)
                {
                    document.Devices.Add(record);
                    if (document.Devices.Count == MaximumSavedDevices)
                        break;
                }

                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(DeviceStoreDocument));
                using (FileStream stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    serializer.WriteObject(stream, document);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(path);
                        File.Move(temporary, path);
                    }
                    catch (IOException)
                    {
                        File.Delete(path);
                        File.Move(temporary, path);
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        public static DeviceDiscoveryResult DiscoverUsb(string adbPath, int timeoutMilliseconds)
        {
            DeviceDiscoveryResult result = new DeviceDiscoveryResult();
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = adbPath;
                info.Arguments = "devices -l";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.UTF8;
                info.StandardErrorEncoding = Encoding.UTF8;
                using (Process process = Process.Start(info))
                {
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try { process.Kill(); }
                        catch { }
                        result.Error = "ADB 设备扫描超时";
                        return result;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd().Trim();
                    if (process.ExitCode != 0)
                    {
                        result.Error = error.Length == 0 ? "ADB 设备扫描失败" : error;
                        return result;
                    }
                    foreach (DiscoveredDevice device in ParseDevices(output))
                        result.Devices.Add(device);
                }

                foreach (DiscoveredDevice device in result.Devices)
                {
                    if (device.State == DeviceAdbState.Online)
                        PopulateProperties(adbPath, device);
                }
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name;
                return result;
            }
        }

        internal static List<DiscoveredDevice> ParseDevices(string output)
        {
            List<DiscoveredDevice> result = new List<DiscoveredDevice>();
            HashSet<string> serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = (output ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;
                string serial = Clean(parts[0], 200);
                string stateText = parts[1].Trim();
                bool explicitUsb = false;
                string modelToken = string.Empty;
                for (int i = 2; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("usb:", StringComparison.OrdinalIgnoreCase))
                        explicitUsb = true;
                    else if (parts[i].StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                        modelToken = parts[i].Substring(6).Replace('_', ' ');
                }
                // Windows ADB commonly omits the `usb:` detail even for a physical
                // cable. Exclude both host:port serials and modern mDNS wireless
                // serials, while retaining ordinary hardware serials.
                bool wireless = serial.IndexOf(':') >= 0 ||
                    (serial.StartsWith("adb-", StringComparison.OrdinalIgnoreCase) &&
                     serial.IndexOf("._adb-tls", StringComparison.OrdinalIgnoreCase) >= 0);
                bool physicalUsb = explicitUsb ||
                    (!wireless && !serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase));
                if (!physicalUsb || serial.Length == 0 || !serials.Add(serial))
                    continue;

                DeviceAdbState state = DeviceAdbState.Offline;
                if (string.Equals(stateText, "device", StringComparison.OrdinalIgnoreCase))
                    state = DeviceAdbState.Online;
                else if (string.Equals(stateText, "unauthorized", StringComparison.OrdinalIgnoreCase))
                    state = DeviceAdbState.Unauthorized;

                result.Add(new DiscoveredDevice
                {
                    Serial = serial,
                    State = state,
                    Name = modelToken.Length == 0 ? "Android 设备" : Clean(modelToken, 80),
                    Model = state == DeviceAdbState.Unauthorized
                        ? "请在手机上允许 USB 调试"
                        : state == DeviceAdbState.Offline ? "ADB 设备离线" : Clean(modelToken, 160),
                    AndroidVersion = string.Empty
                });
            }
            return result;
        }

        private static void PopulateProperties(string adbPath, DiscoveredDevice device)
        {
            AdbResult properties = new AdbClient(adbPath, device.Serial).Shell(
                "getprop ro.product.marketname; " +
                "getprop ro.vendor.oplus.market.name; " +
                "getprop ro.product.manufacturer; " +
                "getprop ro.product.model; " +
                "getprop ro.build.version.release",
                6000);
            if (!properties.Success)
                return;

            string[] lines = (properties.Output ?? string.Empty)
                .Replace("\r", string.Empty)
                .Split(new char[] { '\n' }, StringSplitOptions.None);
            string marketName = Line(lines, 0);
            string alternateMarketName = Line(lines, 1);
            string manufacturer = Line(lines, 2);
            string model = Line(lines, 3);
            string android = Line(lines, 4);
            string displayName = FirstNonEmpty(marketName, alternateMarketName, model, device.Name);
            string modelName = FirstNonEmpty(model, device.Model, "Android 设备");
            if (manufacturer.Length > 0 &&
                modelName.IndexOf(manufacturer, StringComparison.OrdinalIgnoreCase) < 0)
                modelName = manufacturer + " " + modelName;
            if (android.Length > 0)
                modelName += " · Android " + android;
            device.Name = Clean(displayName, 80);
            device.Model = Clean(modelName, 160);
            device.AndroidVersion = Clean(android, 40);
        }

        private static string Line(string[] lines, int index)
        {
            return index < lines.Length ? Clean(lines[index], 160) : string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return string.Empty;
        }

        private static string Clean(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            StringBuilder result = new StringBuilder();
            foreach (char character in value.Trim())
            {
                if (!char.IsControl(character))
                    result.Append(character);
                if (result.Length == maximumLength)
                    break;
            }
            return result.ToString();
        }

        public static bool RunSelfTest(string adbPath, string scrcpyPath)
        {
            if (!File.Exists(adbPath) || !File.Exists(scrcpyPath))
                return false;
            if (!CanRun(adbPath, "version", 5000) ||
                !CanRun(scrcpyPath, "--version", 5000))
                return false;

            string fixture =
                "List of devices attached\n" +
                // Windows may omit `usb:` for a physical cable.
                "USB123 device product:test model:Pixel_9 device:test transport_id:1\n" +
                "USB456 unauthorized usb:1-2\n" +
                "USB789 offline usb:1-3\n" +
                "192.168.1.10:5555 device product:wifi model:Wifi_Phone device:wifi\n" +
                "adb-WIFI123._adb-tls-connect._tcp device product:wifi model:Wifi_TLS device:wifi\n" +
                "emulator-5554 device product:sdk model:Emulator device:generic\n" +
                "USB123 device product:test model:Duplicate device:test usb:1-1\n";
            List<DiscoveredDevice> parsed = ParseDevices(fixture);
            if (parsed.Count != 3 || parsed[0].Serial != "USB123" ||
                parsed[0].State != DeviceAdbState.Online ||
                parsed[1].State != DeviceAdbState.Unauthorized ||
                parsed[2].State != DeviceAdbState.Offline)
                return false;

            string directory = Path.Combine(
                Path.GetTempPath(),
                "OMirror-device-self-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "devices.json");
            try
            {
                List<SavedDeviceRecord> records = new List<SavedDeviceRecord>();
                records.Add(new SavedDeviceRecord
                {
                    Serial = "TEST-SERIAL",
                    Name = "测试设备",
                    Model = "Model · Android 15",
                    AndroidVersion = "15",
                    WindowX = 80,
                    WindowY = 90
                });
                Save(path, records);
                List<SavedDeviceRecord> loaded = Load(path);
                if (loaded.Count != 1 ||
                    loaded[0].Serial != "TEST-SERIAL" ||
                    loaded[0].Name != "测试设备" ||
                    loaded[0].WindowY != 90)
                    return false;

                // Exercise replacement of an existing store and load-time duplicate
                // suppression, which is also the restart-restore path used by the UI.
                records.Add(new SavedDeviceRecord
                {
                    Serial = "test-serial",
                    Name = "重复项",
                    Model = "Duplicate",
                    WindowX = 999,
                    WindowY = 999
                });
                records.Add(new SavedDeviceRecord
                {
                    Serial = "SECOND-SERIAL",
                    Name = "第二台设备",
                    Model = "Model 2",
                    AndroidVersion = "14",
                    WindowX = 120,
                    WindowY = 130
                });
                Save(path, records);
                loaded = Load(path);
                return loaded.Count == 2 &&
                    loaded[0].Serial == "TEST-SERIAL" &&
                    loaded[0].Name == "测试设备" &&
                    loaded[1].Serial == "SECOND-SERIAL" &&
                    loaded[1].WindowY == 130;
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch { }
            }
        }

        private static bool CanRun(string executable, string arguments, int timeoutMilliseconds)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = executable;
                info.Arguments = arguments;
                info.WorkingDirectory = Path.GetDirectoryName(executable);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                using (Process process = Process.Start(info))
                {
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try { process.Kill(); }
                        catch { }
                        return false;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    return process.ExitCode == 0 &&
                        (output.Length > 0 || error.Length > 0);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
