using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ThroneForge.LoaderSmokeTest;

public static class LaunchObservationService
{
    public static LaunchObservationResult Observe(
        string executablePath,
        string cleanGameRoot,
        string experimentRoot,
        TimeSpan observationWindow,
        TimeSpan gracefulCloseWindow,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var executable = SmokeTestPathValidator.ValidateExecutablePath(executablePath, cleanGameRoot, experimentRoot);
        var startedAt = Stopwatch.GetTimestamp();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = cleanGameRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                if (string.IsNullOrWhiteSpace(environmentVariable.Key)
                    || environmentVariable.Key.Any(char.IsControl)
                    || environmentVariable.Value.Any(char.IsControl))
                {
                    throw new SmokeTestException("The launch environment contains an invalid variable.");
                }

                process.StartInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        try
        {
            if (!process.Start())
            {
                return Result(startedAt, false, false, false, null, false, "process-not-started");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var executableWasInside = false;
            try
            {
                var actualPath = TryGetProcessPath(process);
                executableWasInside = actualPath is not null
                    && IsWithin(experimentRoot, actualPath);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                var closed = TryClose(process, gracefulCloseWindow);
                return Result(startedAt, true, false, false, null, false, closed ? "process-path-unavailable" : "manual-closure-required");
            }

            if (!executableWasInside)
            {
                TryClose(process, gracefulCloseWindow);
                return Result(startedAt, true, false, false, null, false, "process-executable-outside-experiment");
            }

            var deadline = DateTime.UtcNow + observationWindow;
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    return Result(startedAt, true, false, true, process.ExitCode, true, "process-exited-during-observation");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }

            if (process.HasExited)
            {
                return Result(startedAt, true, false, true, process.ExitCode, true, "process-exited-during-observation");
            }

            if (!TryClose(process, gracefulCloseWindow))
            {
                return Result(startedAt, true, true, false, null, true, "manual-closure-required");
            }

            return Result(startedAt, true, true, true, process.ExitCode, true, "graceful-close");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return Result(startedAt, false, false, false, null, false, "launch-failed");
        }
    }

    public static string ValidateExecutablePath(string executablePath, string cleanGameRoot, string experimentRoot)
        => SmokeTestPathValidator.ValidateExecutablePath(executablePath, cleanGameRoot, experimentRoot);

    private static bool TryClose(Process process, TimeSpan gracefulCloseWindow)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }

            if (!process.CloseMainWindow())
            {
                return false;
            }

            return process.WaitForExit((int)Math.Max(0, gracefulCloseWindow.TotalMilliseconds));
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            var managedPath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(managedPath))
            {
                return managedPath;
            }
        }
        catch (System.ComponentModel.Win32Exception) when (OperatingSystem.IsWindows())
        {
            // A 32-bit observation host may not read a 64-bit module list; use the native path query below.
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var buffer = new StringBuilder(32 * 1024);
        var length = buffer.Capacity;
        return QueryFullProcessImageName(process.Handle, 0, buffer, ref length)
            ? buffer.ToString()
            : null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1838",
        Justification = "QueryFullProcessImageName requires a mutable Windows buffer on the supported x86 observation host.")]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        int flags,
        StringBuilder exeName,
        ref int size);

    private static bool IsWithin(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison)
            || string.Equals(normalizedCandidate, normalizedRoot, comparison);
    }

    private static LaunchObservationResult Result(
        long startedAt,
        bool started,
        bool stable,
        bool exited,
        int? exitCode,
        bool inside,
        string category)
        => new(
            started,
            stable,
            exited,
            exitCode,
            inside,
            category == "manual-closure-required",
            Stopwatch.GetElapsedTime(startedAt),
            category);
}
