// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Threading;
    using static Pty.Net.Windows.NativeMethods;
    using SafeWaitHandle = Microsoft.Win32.SafeHandles.SafeWaitHandle;

    /// <summary>
    /// Tracks the lifetime of a spawned process through the process handle that was returned
    /// when it was created, instead of re-opening the process by pid.
    /// </summary>
    /// <remarks>
    /// A handle that was open before the process exited keeps the process object alive, so its
    /// exit code stays readable after the exit. <see cref="System.Diagnostics.Process.GetProcessById(int)"/>
    /// has no such guarantee: it throws <see cref="ArgumentException"/> once the pid is gone, which
    /// is exactly what happens when a short-lived child finishes before the connection is set up.
    /// </remarks>
    internal sealed class ProcessExitWatcher : IDisposable
    {
        private readonly SafeProcessHandle processHandle;
        private readonly Action<int> onExited;
        private readonly object exitLock = new object();
        private readonly ProcessWaitHandle processWaitHandle;
        private readonly RegisteredWaitHandle registration;
        private int exitCode;
        private bool hasExited;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessExitWatcher"/> class.
        /// </summary>
        /// <param name="processHandle">The handle returned when the process was created. The watcher does not take ownership of it.</param>
        /// <param name="onExited">Invoked once, on a thread pool thread, with the exit code. May run before the constructor returns if the process has already exited.</param>
        public ProcessExitWatcher(SafeProcessHandle processHandle, Action<int> onExited)
        {
            this.processHandle = processHandle;
            this.onExited = onExited;

            this.processWaitHandle = new ProcessWaitHandle(processHandle);
            this.registration = ThreadPool.RegisterWaitForSingleObject(
                this.processWaitHandle,
                this.OnSignaled,
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);
        }

        /// <summary>
        /// Gets the exit code of the process.
        /// </summary>
        /// <exception cref="InvalidOperationException">The process has not exited.</exception>
        public int ExitCode
        {
            get
            {
                if (!this.WaitForExit(0))
                {
                    throw new InvalidOperationException("The process has not exited.");
                }

                lock (this.exitLock)
                {
                    return this.exitCode;
                }
            }
        }

        /// <summary>
        /// Waits for the process to exit.
        /// </summary>
        /// <param name="milliseconds">Time to wait, or -1 to wait indefinitely.</param>
        /// <returns>true if the process has exited; false on timeout.</returns>
        public bool WaitForExit(int milliseconds)
        {
            lock (this.exitLock)
            {
                if (this.hasExited)
                {
                    return true;
                }
            }

            uint result = WaitForSingleObject(this.processHandle, milliseconds < 0 ? INFINITE : (uint)milliseconds);
            switch (result)
            {
                case WAIT_OBJECT_0:
                    this.ReadExitCode();
                    return true;
                case WAIT_TIMEOUT:
                    return false;
                default:
                    throw new Win32Exception();
            }
        }

        /// <summary>
        /// Terminates the process. Does nothing if it has already exited.
        /// </summary>
        public void Kill()
        {
            if (this.WaitForExit(0))
            {
                return;
            }

            if (!TerminateProcess(this.processHandle, unchecked((uint)-1)))
            {
                int error = Marshal.GetLastWin32Error();

                // The process may have exited between the check above and the call.
                if (this.WaitForExit(0))
                {
                    return;
                }

                throw new Win32Exception(error);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (this.exitLock)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
            }

            this.registration.Unregister(null);
            this.processWaitHandle.Dispose();
        }

        private void ReadExitCode()
        {
            lock (this.exitLock)
            {
                if (this.hasExited)
                {
                    return;
                }

                if (!GetExitCodeProcess(this.processHandle, out int code))
                {
                    throw new Win32Exception();
                }

                this.exitCode = code;
                this.hasExited = true;
            }
        }

        private void OnSignaled(object? state, bool timedOut)
        {
            int code;
            lock (this.exitLock)
            {
                if (this.disposed)
                {
                    return;
                }
            }

            try
            {
                this.ReadExitCode();
                lock (this.exitLock)
                {
                    code = this.exitCode;
                }
            }
            catch (ObjectDisposedException)
            {
                // The connection was disposed while the exit was being observed.
                return;
            }

            this.onExited(code);
        }

        /// <summary>
        /// A process handle is a waitable kernel object. This wraps it, without taking ownership,
        /// so the thread pool can wait on it; the process handle stays owned by the connection.
        /// </summary>
        private sealed class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(SafeProcessHandle processHandle)
            {
                this.SafeWaitHandle = new SafeWaitHandle(processHandle.Handle, ownsHandle: false);
            }
        }
    }
}
