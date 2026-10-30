// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Tests
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// A child that exits before the connection is set up must still come back as a connection
    /// that reports its exit code (aaasoft/Quick.PtyNet#3).
    /// </summary>
    /// <remarks>
    /// The attach is slowest on the FIRST spawn in a process, which is when it loses the race
    /// against an instantly exiting child. This test is therefore only a faithful reproduction
    /// when it is the first pty spawned in the test host, i.e. when run on its own:
    /// <c>dotnet test --filter FullyQualifiedName~FastExitingChildTests</c>.
    /// In a host that has already spawned a pty it passes even on the unfixed code.
    /// The command lines are verbatim and include the program name, because the WinPTY path
    /// passes <see cref="PtyOptions.CommandLine"/> to the child as its complete command line.
    /// </remarks>
    public class FastExitingChildTests
    {
        [WindowsFact]
        public async Task FirstSpawnOfInstantlyExitingChildReportsExitCode()
        {
            var options = new PtyOptions
            {
                Name = "fast exit",
                Cols = 80,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                CommandLine = new[] { "cmd.exe", "/c", "exit 3" },
                VerbatimCommandLine = true,
                ForceWinPty = true,
            };

            using (var cts = new CancellationTokenSource(10_000))
            using (IPtyConnection terminal = await PtyProvider.SpawnAsync(options, cts.Token))
            {
                // The exit may already have been observed before this subscription exists, so
                // the contract under test is WaitForExit + ExitCode, not the event.
                Assert.True(terminal.WaitForExit(10_000), "the child did not exit within the deadline");
                Assert.Equal(3, terminal.ExitCode);
            }
        }

        [WindowsFact]
        public async Task LongLivedChildStillReportsExitCodeAndEvent()
        {
            var options = new PtyOptions
            {
                Name = "lives then exits",
                Cols = 80,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                CommandLine = new[] { "cmd.exe", "/c", "ping -n 2 127.0.0.1 >nul & exit 3" },
                VerbatimCommandLine = true,
                ForceWinPty = true,
            };

            using (var cts = new CancellationTokenSource(20_000))
            using (IPtyConnection terminal = await PtyProvider.SpawnAsync(options, cts.Token))
            {
                var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                terminal.ProcessExited += (sender, e) => exited.TrySetResult(e.ExitCode);

                Assert.True(terminal.WaitForExit(20_000), "the child did not exit within the deadline");
                Assert.Equal(3, terminal.ExitCode);

                Task finished = await Task.WhenAny(exited.Task, Task.Delay(5_000));
                Assert.True(finished == exited.Task, "ProcessExited was not raised");
                Assert.Equal(3, await exited.Task);
            }
        }

        [WindowsFact]
        public async Task KillTerminatesLongLivedChild()
        {
            var options = new PtyOptions
            {
                Name = "killed",
                Cols = 80,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                CommandLine = new[] { "cmd.exe", "/c", "ping -n 30 127.0.0.1 >nul" },
                VerbatimCommandLine = true,
                ForceWinPty = true,
            };

            using (var cts = new CancellationTokenSource(20_000))
            using (IPtyConnection terminal = await PtyProvider.SpawnAsync(options, cts.Token))
            {
                Assert.False(terminal.WaitForExit(0));
                terminal.Kill();
                Assert.True(terminal.WaitForExit(10_000), "the child did not exit after Kill");
                Assert.Equal(-1, terminal.ExitCode);

                // Killing an exited process is a no-op, as with System.Diagnostics.Process.
                terminal.Kill();
            }
        }
    }

    /// <summary>
    /// A fact that only runs on Windows.
    /// </summary>
    public sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "Windows only";
            }
        }
    }
}
