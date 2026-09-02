using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace OPhoneMirror
{
    internal sealed class AdbResult
    {
        public int ExitCode;
        public string Output;
        public string Error;

        public bool Success
        {
            get { return ExitCode == 0; }
        }

        public string Message
        {
            get
            {
                string error = (Error ?? string.Empty).Trim();
                if (error.Length > 0)
                    return error;
                return (Output ?? string.Empty).Trim();
            }
        }
    }

    internal sealed class AdbClient
    {
        private readonly string adbPath;
        private readonly string serial;

        public AdbClient(string adbPath, string serial)
        {
            this.adbPath = adbPath;
            this.serial = serial;
        }

        public AdbResult RunDevice(string arguments, int timeoutMilliseconds)
        {
            return Run("-s " + QuoteWindowsArgument(serial) + " " + arguments, timeoutMilliseconds);
        }

        public AdbResult Shell(string command, int timeoutMilliseconds)
        {
            return RunDevice("shell " + QuoteWindowsArgument(command), timeoutMilliseconds);
        }

        public AdbResult ShellUtf8(string command, int timeoutMilliseconds)
        {
            return Run(
                "-s " + QuoteWindowsArgument(serial) + " shell",
                timeoutMilliseconds,
                command + "\n");
        }

        public AdbResult Push(string localPath, string remoteDirectory)
        {
            string target = NormalizeRemotePath(remoteDirectory);
            if (!target.EndsWith("/", StringComparison.Ordinal))
                target += "/";
            return RunDevice(
                "push " + QuoteWindowsArgument(localPath) + " " + QuoteWindowsArgument(target),
                0);
        }

        public AdbResult Pull(string remotePath, string localDirectory)
        {
            return RunDevice(
                "pull " + QuoteWindowsArgument(NormalizeRemotePath(remotePath)) + " " +
                QuoteWindowsArgument(localDirectory),
                0);
        }

        public bool RemoteExists(string remotePath)
        {
            return ShellUtf8("[ -e " + ShellQuote(NormalizeRemotePath(remotePath)) + " ]", 5000).Success;
        }

        public AdbResult PushPreservingName(
            string localPath,
            string remoteDirectory,
            string destinationName,
            bool isDirectory,
            bool overwrite)
        {
            string directory = NormalizeRemotePath(remoteDirectory);
            string finalPath = JoinRemotePath(directory, destinationName);
            string temporaryPath = JoinRemotePath(
                directory,
                ".phonemirror-upload-" + Guid.NewGuid().ToString("N"));

            if (!overwrite && RemoteExists(finalPath))
            {
                return new AdbResult
                {
                    ExitCode = 1,
                    Error = "手机目标已存在：" + finalPath
                };
            }

            AdbResult push = RunDevice(
                "push " + QuoteWindowsArgument(localPath) + " " + QuoteWindowsArgument(temporaryPath),
                0);
            if (!push.Success)
            {
                ShellUtf8("rm -rf " + ShellQuote(temporaryPath), 5000);
                return push;
            }

            string finalize;
            if (isDirectory)
            {
                finalize =
                    "if [ -e " + ShellQuote(finalPath) + " ]; then " +
                    "if [ -d " + ShellQuote(finalPath) + " ]; then " +
                    "cp -a " + ShellQuote(temporaryPath + "/.") + " " + ShellQuote(finalPath + "/") +
                    " && rm -rf " + ShellQuote(temporaryPath) + "; " +
                    "else rm -f " + ShellQuote(finalPath) + " && mv " + ShellQuote(temporaryPath) + " " + ShellQuote(finalPath) + "; fi; " +
                    "else mv " + ShellQuote(temporaryPath) + " " + ShellQuote(finalPath) + "; fi";
            }
            else
            {
                finalize =
                    "if [ -e " + ShellQuote(finalPath) + " ]; then rm -rf " + ShellQuote(finalPath) + " || exit 1; fi; " +
                    "mv " + ShellQuote(temporaryPath) + " " + ShellQuote(finalPath);
            }

            AdbResult move = ShellUtf8(finalize, 30000);
            if (!move.Success)
            {
                ShellUtf8("rm -rf " + ShellQuote(temporaryPath), 5000);
                return move;
            }

            return new AdbResult
            {
                ExitCode = 0,
                Output = push.Message,
                Error = string.Empty
            };
        }

        public AdbResult PullPreservingName(
            string remotePath,
            string localDirectory,
            string destinationName,
            bool isDirectory,
            bool overwrite)
        {
            string baseDirectory = Path.GetFullPath(localDirectory);
            string finalPath = Path.GetFullPath(Path.Combine(baseDirectory, destinationName));
            string safePrefix = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!finalPath.StartsWith(safePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new AdbResult
                {
                    ExitCode = 1,
                    Error = "接收路径越出了所选电脑目录"
                };
            }

            bool exists = File.Exists(finalPath) || Directory.Exists(finalPath);
            if (exists && !overwrite)
            {
                return new AdbResult
                {
                    ExitCode = 1,
                    Error = "电脑目标已存在：" + finalPath
                };
            }

            string temporaryPath = Path.Combine(
                baseDirectory,
                ".phonemirror-pull-" + Guid.NewGuid().ToString("N"));
            AdbResult pull = isDirectory
                ? PullDirectoryWithTar(NormalizeRemotePath(remotePath), temporaryPath)
                : RunDevice(
                    "pull " + QuoteWindowsArgument(NormalizeRemotePath(remotePath)) + " " +
                    QuoteWindowsArgument(temporaryPath),
                    0);
            if (!pull.Success)
            {
                DeleteLocalTemporary(temporaryPath);
                return pull;
            }

            try
            {
                if (exists)
                {
                    if (Directory.Exists(finalPath))
                        Directory.Delete(finalPath, true);
                    else
                        File.Delete(finalPath);
                }

                if (isDirectory || Directory.Exists(temporaryPath))
                    Directory.Move(temporaryPath, finalPath);
                else
                    File.Move(temporaryPath, finalPath);
            }
            catch (Exception ex)
            {
                DeleteLocalTemporary(temporaryPath);
                return new AdbResult
                {
                    ExitCode = 1,
                    Error = ex.Message
                };
            }

            return new AdbResult
            {
                ExitCode = 0,
                Output = pull.Message,
                Error = string.Empty
            };
        }

        private AdbResult PullDirectoryWithTar(string remotePath, string temporaryPath)
        {
            string tarPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "tar.exe");
            if (!File.Exists(tarPath))
            {
                return new AdbResult
                {
                    ExitCode = 1,
                    Error = "Windows tar.exe 不存在，无法接收文件夹"
                };
            }

            Directory.CreateDirectory(temporaryPath);

            ProcessStartInfo adbInfo = new ProcessStartInfo();
            adbInfo.FileName = adbPath;
            adbInfo.Arguments =
                "-s " + QuoteWindowsArgument(serial) +
                " exec-out tar -C " + QuoteWindowsArgument(remotePath) + " -cf - .";
            adbInfo.UseShellExecute = false;
            adbInfo.CreateNoWindow = true;
            adbInfo.RedirectStandardOutput = true;
            adbInfo.RedirectStandardError = true;
            adbInfo.StandardErrorEncoding = Encoding.UTF8;

            ProcessStartInfo tarInfo = new ProcessStartInfo();
            tarInfo.FileName = tarPath;
            tarInfo.Arguments =
                "-xf - --options hdrcharset=UTF-8 -C " + QuoteWindowsArgument(temporaryPath);
            tarInfo.UseShellExecute = false;
            tarInfo.CreateNoWindow = true;
            tarInfo.RedirectStandardInput = true;
            tarInfo.RedirectStandardOutput = true;
            tarInfo.RedirectStandardError = true;
            tarInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            tarInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            string adbError = string.Empty;
            string tarError = string.Empty;
            string copyError = string.Empty;

            using (Process tar = Process.Start(tarInfo))
            using (Process adb = Process.Start(adbInfo))
            {
                Thread adbErrorReader = new Thread(new ThreadStart(delegate
                {
                    adbError = adb.StandardError.ReadToEnd();
                }));
                Thread tarErrorReader = new Thread(new ThreadStart(delegate
                {
                    tarError = tar.StandardError.ReadToEnd();
                }));
                Thread tarOutputReader = new Thread(new ThreadStart(delegate
                {
                    tar.StandardOutput.ReadToEnd();
                }));
                Thread copyThread = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        adb.StandardOutput.BaseStream.CopyTo(tar.StandardInput.BaseStream);
                        tar.StandardInput.BaseStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        copyError = ex.Message;
                    }
                    finally
                    {
                        try { tar.StandardInput.BaseStream.Close(); }
                        catch { }
                    }
                }));

                adbErrorReader.IsBackground = true;
                tarErrorReader.IsBackground = true;
                tarOutputReader.IsBackground = true;
                copyThread.IsBackground = true;
                adbErrorReader.Start();
                tarErrorReader.Start();
                tarOutputReader.Start();
                copyThread.Start();

                adb.WaitForExit();
                copyThread.Join();
                tar.WaitForExit();
                adbErrorReader.Join(2000);
                tarErrorReader.Join(2000);
                tarOutputReader.Join(2000);

                if (adb.ExitCode != 0 || tar.ExitCode != 0 || copyError.Length > 0)
                {
                    DeleteLocalTemporary(temporaryPath);
                    string error = (adbError + "\n" + tarError + "\n" + copyError).Trim();
                    return new AdbResult
                    {
                        ExitCode = adb.ExitCode != 0 ? adb.ExitCode : tar.ExitCode,
                        Error = error.Length == 0 ? "文件夹传输失败" : error
                    };
                }
            }

            return new AdbResult
            {
                ExitCode = 0,
                Output = "文件夹已完整接收",
                Error = string.Empty
            };
        }

        private static void DeleteLocalTemporary(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        public AdbResult ListRemote(string remoteDirectory)
        {
            string command =
                "find " + ShellQuote(NormalizeRemotePath(remoteDirectory)) +
                " -mindepth 1 -maxdepth 1 -printf '%M|%s|%T@|%p\\n'";
            return Shell(command, 15000);
        }

        private AdbResult Run(string arguments, int timeoutMilliseconds)
        {
            return Run(arguments, timeoutMilliseconds, null);
        }

        private AdbResult Run(string arguments, int timeoutMilliseconds, string standardInput)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = adbPath;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = standardInput != null;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = Process.Start(psi))
            {
                if (standardInput != null)
                {
                    byte[] inputBytes = new UTF8Encoding(false).GetBytes(standardInput);
                    process.StandardInput.BaseStream.Write(inputBytes, 0, inputBytes.Length);
                    process.StandardInput.BaseStream.Flush();
                    process.StandardInput.BaseStream.Close();
                }
                string output = string.Empty;
                string error = string.Empty;
                Thread outputReader = new Thread(new ThreadStart(delegate
                {
                    output = process.StandardOutput.ReadToEnd();
                }));
                Thread errorReader = new Thread(new ThreadStart(delegate
                {
                    error = process.StandardError.ReadToEnd();
                }));
                outputReader.IsBackground = true;
                errorReader.IsBackground = true;
                outputReader.Start();
                errorReader.Start();

                bool exited = timeoutMilliseconds <= 0
                    ? WaitWithoutTimeout(process)
                    : process.WaitForExit(timeoutMilliseconds);

                if (!exited)
                {
                    try { process.Kill(); }
                    catch { }
                    try { process.WaitForExit(2000); }
                    catch { }
                    outputReader.Join(2000);
                    errorReader.Join(2000);
                    return new AdbResult
                    {
                        ExitCode = -1,
                        Output = output,
                        Error = "ADB 操作超时"
                    };
                }

                outputReader.Join(2000);
                errorReader.Join(2000);

                return new AdbResult
                {
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error
                };
            }
        }

        private static bool WaitWithoutTimeout(Process process)
        {
            process.WaitForExit();
            return true;
        }

        public static string NormalizeRemotePath(string path)
        {
            string normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (normalized.Length == 0)
                return "/sdcard/Download";
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = "/" + normalized;
            while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 1);
            return normalized;
        }

        public static string JoinRemotePath(string directory, string name)
        {
            string normalized = NormalizeRemotePath(directory);
            if (normalized == "/")
                return "/" + name;
            return normalized + "/" + name;
        }

        public static string RemoteParent(string path)
        {
            string normalized = NormalizeRemotePath(path);
            if (normalized == "/")
                return "/";
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? "/" : normalized.Substring(0, slash);
        }

        public static string ShellQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\"'\"'") + "'";
        }

        public static string QuoteWindowsArgument(string value)
        {
            if (value == null)
                return "\"\"";

            StringBuilder result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;

            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(c);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
