using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceAttackDiscordPlugin.Commands;

internal static class KeyboardAutomation
{
    internal const byte VK_CONTROL = 0x11;
    internal const byte VK_SHIFT = 0x10;
    internal const byte VK_RETURN = 0x0D;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    internal static extern short VkKeyScan(char ch);

    internal static byte VkFor(char ch)
    {
        return (byte)(VkKeyScan(ch) & 0xFF);
    }

    internal static void KeyDown(byte vk) => keybd_event(vk, 0, 0, UIntPtr.Zero);

    internal static void KeyUp(byte vk) => keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

    internal static void PressKey(byte vk)
    {
        KeyDown(vk);
        KeyUp(vk);
    }

    internal static void PressCombo(params byte[] vks)
    {
        foreach (byte vk in vks) KeyDown(vk);
        for (int i = vks.Length - 1; i >= 0; i--) KeyUp(vks[i]);
    }

    internal static async Task TypeTextAsync(string text)
    {
        foreach (char ch in text)
        {
            short scan = VkKeyScan(ch);
            if (scan == -1) continue;

            byte vk = (byte)(scan & 0xFF);
            bool shift = (scan & 0x100) != 0;

            if (shift) KeyDown(VK_SHIFT);
            PressKey(vk);
            if (shift) KeyUp(VK_SHIFT);

            await Task.Delay(15);
        }
    }

    internal static IntPtr FindDiscordWindow(dynamic va)
    {
        var discordProcess = Process.GetProcessesByName("Discord").FirstOrDefault();
        if (discordProcess == null)
        {
            va.WriteToLog("Discord: Discord desktop app not found. Please open Discord first.", "red");
            return IntPtr.Zero;
        }

        if (!LooksLikeDiscordInstall(discordProcess))
        {
            va.WriteToLog("Discord: Found a process named Discord outside a Discord install folder — refusing to automate it.", "red");
            return IntPtr.Zero;
        }

        if (discordProcess.MainWindowHandle == IntPtr.Zero)
        {
            va.WriteToLog("Discord: Discord window not visible. Please open Discord first.", "red");
            return IntPtr.Zero;
        }

        return discordProcess.MainWindowHandle;
    }

    private static bool LooksLikeDiscordInstall(Process process)
    {
        try
        {
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return true;
            string? dir = Path.GetDirectoryName(path);
            return string.Equals(Path.GetFileName(path), "Discord.exe", StringComparison.OrdinalIgnoreCase)
                && dir != null && dir.Contains("Discord", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }
}
