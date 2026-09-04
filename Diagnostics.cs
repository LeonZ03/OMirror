using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OPhoneMirror
{
    internal static class Diagnostics
    {
        private static readonly object Sync = new object();
        private static readonly Queue<string> Recent = new Queue<string>();
        private const int RecentLimit = 240;
        private static bool debug;

        public static bool DebugEnabled
        {
            get { return debug; }
        }

        public static void Configure(bool enableDebug)
        {
            debug = enableDebug;
        }

        public static void Trace(string deviceName, string eventName, string detail)
        {
            string line = string.Format(
                "{0:O}\t{1}\t{2}\t{3}",
                DateTime.UtcNow,
                Clean(deviceName),
                Clean(eventName),
                Clean(detail));
            lock (Sync)
            {
                Recent.Enqueue(line);
                while (Recent.Count > RecentLimit)
                    Recent.Dequeue();
            }
        }

        public static void FlushUnexpectedExit(string deviceName, string serial, int exitCode)
        {
            try
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OPhoneMirror",
                    "Diagnostics");
                Directory.CreateDirectory(root);
                Trim(root);

                string path = Path.Combine(root,
                    "unexpected-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                StringBuilder text = new StringBuilder();
                text.AppendLine("OPhoneMirror unexpected scrcpy exit");
                text.AppendLine("device=" + Clean(deviceName));
                text.AppendLine("exitCode=" + exitCode);
                lock (Sync)
                {
                    foreach (string line in Recent)
                        text.AppendLine(Redact(line, serial));
                }
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // Diagnostics must never destabilize the mirror session.
            }
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");
        }

        private static string Redact(string value, string serial)
        {
            if (!string.IsNullOrWhiteSpace(serial))
                value = value.Replace(serial, "<device>");
            return value;
        }

        private static void Trim(string root)
        {
            string[] files = Directory.GetFiles(root, "*.log");
            Array.Sort(files, delegate(string left, string right)
            {
                return File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left));
            });
            for (int i = 5; i < files.Length; i++)
            {
                try { File.Delete(files[i]); }
                catch { }
            }
        }
    }
}
