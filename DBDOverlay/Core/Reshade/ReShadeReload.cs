using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DBDOverlay.Core.Reshade
{
    public static class ReShadeReload
    {
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);

        public static string FindConfig()
        {
            foreach (var name in new[] { "DeadByDaylight-Win64-Shipping", "DeadByDaylight-EGS-Shipping" })
                foreach (var process in Process.GetProcessesByName(name))
                    using (process)
                    {
                        try { var path = Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "ReShade.ini"); if (File.Exists(path)) return path; }
                        catch (System.ComponentModel.Win32Exception) { }
                        catch (InvalidOperationException) { }
                    }
            var steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "Dead by Daylight", "DeadByDaylight", "Binaries", "Win64", "ReShade.ini");
            return File.Exists(steam) ? steam : "";
        }

        public static ReloadResult TrySend(string configPath, string preset)
        {
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var pid);
            if (pid == 0) return ReloadResult.Waiting;
            using (var process = Process.GetProcessById((int)pid))
            {
                if (process.ProcessName != "DeadByDaylight-Win64-Shipping" && process.ProcessName != "DeadByDaylight-EGS-Shipping") return ReloadResult.Waiting;
                if (!Path.GetDirectoryName(process.MainModule.FileName).Equals(Path.GetDirectoryName(Path.GetFullPath(configPath)), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Select ReShade.ini from the running game's folder.");
            }
            var config = ReloadConfiguration.Parse(File.ReadAllText(configPath), configPath, preset);
            foreach (var key in new[] { 16, 17, 18, 91, 92, (int)config.Key }) if ((GetAsyncKeyState(key) & 0x8000) != 0) return ReloadResult.Waiting;
            if (GetForegroundWindow() != foreground) return ReloadResult.Waiting;
            var down = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = config.Key } } };
            var up = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = config.Key, Flags = 2 } } };
            // ReShade samples key state on rendered frames; down/up in one batch may be missed.
            return ReloadKeyStroke.Pulse(pressed => SendInput(1, new[] { pressed ? down : up }, Marshal.SizeOf(typeof(Input))) == 1,
                () => System.Threading.Thread.Sleep(80));
        }
    }
}
