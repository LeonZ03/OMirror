using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
class ScreenShortcutProbe {
    [DllImport("SDL3.dll")] static extern bool SDL_Init(uint flags);
    [DllImport("SDL3.dll")] static extern IntPtr SDL_CreateWindow(string title, int w, int h, ulong flags);
    [DllImport("SDL3.dll")] static extern uint SDL_GetWindowProperties(IntPtr window);
    [DllImport("SDL3.dll")] static extern IntPtr SDL_GetPointerProperty(uint props, string name, IntPtr fallback);
    [DllImport("SDL3.dll")] static extern bool SDL_PollEvent(IntPtr data);
    [DllImport("SDL3.dll")] static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport("SDL3.dll")] static extern bool SDL_HideWindow(IntPtr window);
    [DllImport("SDL3.dll")] static extern void SDL_Quit();
    static int Main(string[] args) {
        if (!SDL_Init(0x20)) return 2;
        IntPtr window = SDL_CreateWindow("OMirror shortcut verification", 280, 80, 0);
        if (window == IntPtr.Zero) return 3;
        IntPtr hwnd = SDL_GetPointerProperty(SDL_GetWindowProperties(window), "SDL.window.win32.hwnd", IntPtr.Zero);
        MethodInfo send = Assembly.LoadFrom(args[0]).GetType("OMirror.MainForm").GetMethod("SendScreenPowerWindowShortcut", BindingFlags.NonPublic | BindingFlags.Static);
        int received = 0;
        bool success = true;
        bool[] wanted = new bool[40];
        for (int i = 0; i < wanted.Length; i++) wanted[i] = (i % 2) != 0;
        Thread worker = new Thread(delegate() {
            Thread.Sleep(300);
            foreach(bool on in wanted) {
                if (!(bool)send.Invoke(null, new object[] { hwnd, on })) success = false;
                Thread.Sleep(30);
            }
        });
        worker.Start();
        IntPtr evt = Marshal.AllocHGlobal(128);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while(DateTime.UtcNow < deadline && (worker.IsAlive || received < wanted.Length)) {
            while(SDL_PollEvent(evt)) {
                if (Marshal.ReadInt32(evt) == 0x300 && Marshal.ReadInt32(evt, 24) == 18) {
                    int mod = (ushort)Marshal.ReadInt16(evt, 32);
                    bool on = (mod & 3) != 0;
                    Console.WriteLine("O event: alt=" + ((mod & 0x100) != 0) + " shift=" + on);
                    if (received >= wanted.Length || on != wanted[received] || (mod & 0x100) == 0) success = false;
                    received++;
                    if (received == 20) SDL_HideWindow(window);
                }
            }
            Thread.Sleep(1);
        }
        worker.Join();
        Marshal.FreeHGlobal(evt);
        SDL_DestroyWindow(window);
        SDL_Quit();
        Console.WriteLine("Events=" + received + " passed=" + (success && received == wanted.Length));
        return success && received == wanted.Length ? 0 : 1;
    }
}
