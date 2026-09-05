using System;
using System.Collections.Generic;
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
        private const int VkCapital = 0x14;
        private const int VkSnapshot = 0x2C;
        private const int VkF4 = 0x73;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;
        private const int VkVolumeMute = 0xAD;
        private const int VkVolumeDown = 0xAE;
        private const int VkVolumeUp = 0xAF;
        private const int VkMediaNext = 0xB0;
        private const int VkMediaPrevious = 0xB1;
        private const int VkMediaStop = 0xB2;
        private const int VkMediaPlayPause = 0xB3;
        private const int LlkhfAltDown = 0x20;

        private readonly MainForm owner;
        private readonly HookProc hookProc;
        private readonly HashSet<int> suppressedHeldKeys = new HashSet<int>();
        private IntPtr hookHandle;
        private IntPtr ownedWindow;
        private DeviceCard ownedDevice;
        private bool tabSuppressed;
        private bool winHeld;
        private bool ctrlEscapeSuppressed;
        private bool altF4Suppressed;
        private bool leftShiftSuppressed;
        private bool rightShiftSuppressed;

        public KeyboardCapture(MainForm owner)
        {
            this.owner = owner;
            hookProc = HookCallback;
            hookHandle = SetWindowsHookEx(WhKeyboardLl, hookProc, GetModuleHandle(null), 0);
        }

        public bool IsInstalled { get { return hookHandle != IntPtr.Zero; } }
        public bool IsPhoneOwned { get { return ownedDevice != null; } }

        public void RefreshOwnership()
        {
            IntPtr foreground = GetForegroundWindow();
            DeviceCard device = owner.GetMirrorDeviceForWindow(foreground);
            if (device == null)
            {
                LeavePhoneOwnership();
                return;
            }

            bool changed = foreground != ownedWindow || device != ownedDevice;
            ownedWindow = foreground;
            ownedDevice = device;
            if (changed)
                Diagnostics.Trace(device.Name, "keyboard-owner-enter", "native-input-context");
        }

        private void LeavePhoneOwnership()
        {
            if (ownedDevice != null)
                Diagnostics.Trace(ownedDevice.Name, "keyboard-owner-leave", "focus-changed");
            ownedWindow = IntPtr.Zero;
            ownedDevice = null;
            ResetSuppressedState();
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
                    bool altDown = (flags & LlkhfAltDown) != 0 ||
                        (GetAsyncKeyState(0x12) & 0x8000) != 0;

                    // In SDK/raw-key mode, consume the single Shift before the
                    // Windows IME sees it and inject the same key on Android.
                    // UHID mode already handles Shift correctly without help.
                    if (owner.UsesShortPhraseKeyboard &&
                        (virtualKey == VkShift || virtualKey == VkLShift || virtualKey == VkRShift))
                    {
                        bool right = virtualKey == VkRShift;
                        bool held = right ? rightShiftSuppressed : leftShiftSuppressed;
                        if (keyDown && !held)
                        {
                            if (right)
                                rightShiftSuppressed = true;
                            else
                                leftShiftSuppressed = true;
                            owner.SendAndroidKeyAsync(device, right ? 60 : 59);
                        }
                        else if (keyUp)
                        {
                            if (right)
                                rightShiftSuppressed = false;
                            else
                                leftShiftSuppressed = false;
                        }
                        return new IntPtr(1);
                    }

                    if (virtualKey == VkTab && (altDown || tabSuppressed))
                    {
                        if (keyDown && !tabSuppressed)
                        {
                            tabSuppressed = true;
                            owner.SendAndroidKeyAsync(device, 187);
                        }
                        else if (keyUp)
                            tabSuppressed = false;
                        return new IntPtr(1);
                    }

                    if (virtualKey == VkLWin || virtualKey == VkRWin)
                    {
                        if (keyDown && !winHeld)
                        {
                            winHeld = true;
                            owner.SendAndroidKeyAsync(device, 3);
                        }
                        else if (keyUp)
                            winHeld = false;
                        return new IntPtr(1);
                    }

                    bool controlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                    if (virtualKey == VkEscape && (controlDown || ctrlEscapeSuppressed))
                    {
                        if (keyDown && !ctrlEscapeSuppressed)
                        {
                            ctrlEscapeSuppressed = true;
                            owner.SendAndroidKeyAsync(device, 3);
                        }
                        else if (keyUp)
                            ctrlEscapeSuppressed = false;
                        return new IntPtr(1);
                    }

                    if (virtualKey == VkF4 && (altDown || altF4Suppressed))
                    {
                        if (keyDown && !altF4Suppressed)
                        {
                            altF4Suppressed = true;
                            owner.SendAndroidKeyAsync(device, 4);
                        }
                        else if (keyUp)
                            altF4Suppressed = false;
                        return new IntPtr(1);
                    }

                    int androidKey;
                    if (TryMapExclusiveSystemKey(virtualKey, out androidKey))
                    {
                        if (keyDown && suppressedHeldKeys.Add(virtualKey))
                            owner.SendAndroidKeyAsync(device, androidKey);
                        else if (keyUp)
                            suppressedHeldKeys.Remove(virtualKey);
                        return new IntPtr(1);
                    }
                }
                else
                    ResetSuppressedState();
            }

            return CallNextHookEx(hookHandle, code, wParam, lParam);
        }

        private static bool TryMapExclusiveSystemKey(int virtualKey, out int androidKey)
        {
            switch (virtualKey)
            {
                case VkCapital: androidKey = 115; return true;
                case VkSnapshot: androidKey = 120; return true;
                case VkVolumeMute: androidKey = 164; return true;
                case VkVolumeDown: androidKey = 25; return true;
                case VkVolumeUp: androidKey = 24; return true;
                case VkMediaNext: androidKey = 87; return true;
                case VkMediaPrevious: androidKey = 88; return true;
                case VkMediaStop: androidKey = 86; return true;
                case VkMediaPlayPause: androidKey = 85; return true;
                default: androidKey = 0; return false;
            }
        }

        private void ResetSuppressedState()
        {
            tabSuppressed = false;
            winHeld = false;
            ctrlEscapeSuppressed = false;
            altF4Suppressed = false;
            leftShiftSuppressed = false;
            rightShiftSuppressed = false;
            suppressedHeldKeys.Clear();
        }

        public void Dispose()
        {
            LeavePhoneOwnership();
            if (hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
