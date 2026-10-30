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
    /// <para>
    /// A handle that was open before the process exited keeps the process object alive, so its
    /// exit code stays readable after the exit. Re-opening by pid with
    /// <see cref="System.Diagnostics.Process.GetProcessById(int)"/> has no such guarantee: once a
    /// short-lived child has finished, the lookup (or arming its Exited event) throws.
    /// </para>
    /// <para>
    /// The watcher owns a duplicate of the spawn handle, independent of the connection's own copy,
    /// and keeps it until the exit has been observed. Exit code and the exit callback therefore
    /// stay available after the connection is disposed, as they were with the
    /// <see cref="System.Diagnostics.Process"/> object this replaces.
    /// </para>
    /// </remarks>
    internal sealed class ProcessExitWatcher
    {
        private readonly SafeProcessHandle processHandle;
        private readonly ProcessWaitHandle processWaitHandle;
        private readonly Action<int> onExited;
        private readonly object exitLock = new object();
        private RegisteredWaitHandle? registration;
        private int exitCode;
        private bool hasExited;
        private bool released;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessExitWatcher"/> class.
        /// </summary>
        /// <param name="spawnHandle">The handle returned when the process was created. It is duplicated; the caller keeps ownership of it.</param>
        /// <param name="onExited">Invoked once, on a thread pool thread, with the exit code. May run before the constructor returns if the process has already exited.</param>
        public ProcessExitWatcher(SafeProcessHandle spawnHandle, Action<int> onExited)
        {
            IntPtr self = GetCurrentProcess();
            if (!DuplicateHandle(self, spawnHandle, self, out SafeProcessHandle ownHandle, 0, false, DUPLICATE_SAME_ACCESS))
            {
                throw new Win32Exception();
            }

            this.processHandle = ownHandle;
            this.onExited = onExited;
            this.processWaitHandle = new ProcessWaitHandle(ownHandle);

            RegisteredWaitHandle waitRegistration = ThreadPool.RegisterWaitForSingleObject(
                this.processWaitHandle,
                this.OnSignaled,
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);

            lock (this.exitLock)
            {
                if (this.released)
                {
                    // The process had already exited and the callback ran before we got here.
                    waitRegistration.Unregister(null);
                }
                else
                {
                    this.registration = waitRegistration;
                }
            }
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

            uint result;
            try
            {
                result = WaitForSingleObject(this.processHandle, milliseconds < 0 ? INFINITE : (uint)milliseconds);
            }
            catch (ObjectDisposedException)
            {
                // The handle is released only after the exit has been recorded.
                return true;
            }

            switch (result)
            {
                case WAIT_OBJECT_0:
                    this.RecordExit();
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

            bool terminated;
            int error;
            try
            {
                terminated = TerminateProcess(this.processHandle, unchecked((uint)-1));
                error = Marshal.GetLastWin32Error();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            // TerminateProcess fails with access denied if the process exited in the meantime.
            if (!terminated && !this.WaitForExit(0))
            {
                throw new Win32Exception(error);
            }
        }

        private void RecordExit()
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
            this.RecordExit();

            int code;
            lock (this.exitLock)
            {
                code = this.exitCode;
                this.released = true;
                this.registration?.Unregister(null);
                this.registration = null;
            }

            // Nothing waits on the handle any more and the exit code is cached.
            this.processWaitHandle.Dispose();
            this.processHandle.Dispose();

            this.onExited(code);
        }

        /// <summary>
        /// A process handle is a waitable kernel object. This wraps the watcher's handle, without
        /// taking ownership, so the thread pool can wait on it.
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
