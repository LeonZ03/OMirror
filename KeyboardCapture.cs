using System;
using System.Runtime.InteropServices;

namespace OPhoneMirror
{
    internal sealed class KeyboardCapture : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const int VkTab = 0x09;
        private const int VkEscape = 0x1B;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;
        private const int VkLShift = 0xA0;
        private const int VkRShift = 0xA1;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;
        private const int LlkhfAltDown = 0x20;

        private readonly MainForm owner;
        private readonly HookProc hookProc;
        private IntPtr hookHandle;
        private bool tabSuppressed;
        private bool winHeld;
        private bool ctrlEscapeSuppressed;
        private bool leftShiftHeld;
        private bool rightShiftHeld;

        public KeyboardCapture(MainForm owner)
        {
            this.owner = owner;
            hookProc = HookCallback;
            hookHandle = SetWindowsHookEx(WhKeyboardLl, hookProc, GetModuleHandle(null), 0);
        }

        public bool IsInstalled
        {
            get { return hookHandle != IntPtr.Zero; }
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                bool keyDown = message == WmKeyDown || message == WmSysKeyDown;
                bool keyUp = message == WmKeyUp || message == WmSysKeyUp;
                int virtualKey = Marshal.ReadInt32(lParam);
                int flags = Marshal.ReadInt32(lParam, 8);
                DeviceCard device = owner.GetFocusedMirrorDevice();

                if (device != null)
                {
                    bool altDown = (flags & LlkhfAltDown) != 0;

                    // Windows handles a bare Shift as an input-language shortcut.
                    // While scrcpy owns focus, consume it before Windows sees it and
                    // send the corresponding Android Shift event instead. The phone
                    // input method therefore remains the only recipient.
                    if (virtualKey == VkShift || virtualKey == VkLShift || virtualKey == VkRShift)
                    {
                        bool rightShift = virtualKey == VkRShift;
                        bool alreadyHeld = rightShift ? rightShiftHeld : leftShiftHeld;
                        if (keyDown && !alreadyHeld)
                        {
                            if (rightShift)
                                rightShiftHeld = true;
                            else
                                leftShiftHeld = true;
                            owner.SendAndroidKeyAsync(device, rightShift ? 60 : 59);
                        }
                        else if (keyUp)
                        {
                            if (rightShift)
                                rightShiftHeld = false;
                            else
                                leftShiftHeld = false;
                        }
                        return new IntPtr(1);
                    }

                    if (virtualKey == VkTab && (altDown || tabSuppressed))
                    {
                        if (keyDown && !tabSuppressed)
                        {
                            tabSuppressed = true;
                            owner.SendAndroidKeyAsync(device, 187); // APP_SWITCH
                        }
                        else if (keyUp)
                        {
                            tabSuppressed = false;
                        }
                        return new IntPtr(1);
                    }

                    if (virtualKey == VkLWin || virtualKey == VkRWin)
                    {
                        if (keyDown && !winHeld)
                        {
                            winHeld = true;
                            owner.SendAndroidKeyAsync(device, 3); // HOME
                        }
                        else if (keyUp)
                        {
                            winHeld = false;
                        }
                        return new IntPtr(1);
                    }

                    bool controlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                    if (virtualKey == VkEscape && (controlDown || ctrlEscapeSuppressed))
                    {
                        if (keyDown && !ctrlEscapeSuppressed)
                        {
                            ctrlEscapeSuppressed = true;
                            owner.SendAndroidKeyAsync(device, 3); // HOME
                        }
                        else if (keyUp)
                        {
                            ctrlEscapeSuppressed = false;
                        }
                        return new IntPtr(1);
                    }
                }
                else
                {
                    tabSuppressed = false;
                    winHeld = false;
                    ctrlEscapeSuppressed = false;
                    leftShiftHeld = false;
                    rightShiftHeld = false;
                }
            }

            return CallNextHookEx(hookHandle, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            HookProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
