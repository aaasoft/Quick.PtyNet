// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Runtime.InteropServices;
    using static Pty.Net.Windows.NativeMethods;

    /// <summary>
    /// A connection to a pseudoterminal spawned by native windows APIs.
    /// </summary>
    internal sealed class PseudoConsoleConnection : IPtyConnection
    {
        private readonly ProcessExitWatcher exitWatcher;
        private PseudoConsoleConnectionHandles handles;

        /// <summary>
        /// Initializes a new instance of the <see cref="PseudoConsoleConnection"/> class.
        /// </summary>
        /// <param name="handles">The set of handles associated with the pseudoconsole.</param>
        public PseudoConsoleConnection(PseudoConsoleConnectionHandles handles)
        {
            this.ReaderStream = new AnonymousPipeClientStream(PipeDirection.In, new Microsoft.Win32.SafeHandles.SafePipeHandle(handles.OutPipeOurSide.Handle, ownsHandle: false));
            this.WriterStream = new AnonymousPipeClientStream(PipeDirection.Out, new Microsoft.Win32.SafeHandles.SafePipeHandle(handles.InPipeOurSide.Handle, ownsHandle: false));

            this.handles = handles;

            // Watch the handle CreateProcess returned instead of re-opening the process by pid;
            // see ProcessExitWatcher for why the pid lookup loses against a fast-exiting child.
            this.exitWatcher = new ProcessExitWatcher(handles.ProcessHandle, this.OnProcessExited);
        }

        /// <inheritdoc/>
        public event EventHandler<PtyExitedEventArgs>? ProcessExited;

        /// <inheritdoc/>
        public Stream ReaderStream { get; }

        /// <inheritdoc/>
        public Stream WriterStream { get; }

        /// <inheritdoc/>
        public int Pid => this.handles.Pid;

        /// <inheritdoc/>
        public int ExitCode => this.exitWatcher.ExitCode;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.ReaderStream?.Dispose();
            this.WriterStream?.Dispose();

            this.exitWatcher.Dispose();
            if (this.handles != null)
            {
                this.handles.PseudoConsoleHandle.Close();
                this.handles.MainThreadHandle.Close();
                this.handles.ProcessHandle.Close();
                this.handles.InPipeOurSide.Close();
                this.handles.InPipePseudoConsoleSide.Close();
                this.handles.OutPipePseudoConsoleSide.Close();
                this.handles.OutPipeOurSide.Close();
            }
        }

        /// <inheritdoc/>
        public void Kill()
        {
            this.exitWatcher.Kill();
        }

        /// <inheritdoc/>
        public void Resize(int cols, int rows)
        {
            int hr = ResizePseudoConsole(this.handles.PseudoConsoleHandle, new Coord(cols, rows));
            if (hr != S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            return this.exitWatcher.WaitForExit(milliseconds);
        }

        private void OnProcessExited(int exitCode)
        {
            this.ProcessExited?.Invoke(this, new PtyExitedEventArgs(exitCode));
        }

        /// <summary>
        /// handles to resources creates when a pseudoconsole is spawned.
        /// </summary>
        internal sealed class PseudoConsoleConnectionHandles
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PseudoConsoleConnectionHandles"/> class.
            /// </summary>
            /// <param name="inPipePseudoConsoleSide">the input pipe on the pseudoconsole side.</param>
            /// <param name="outPipePseudoConsoleSide">the output pipe on the pseudoconsole side.</param>
            /// <param name="inPipeOurSide"> the input pipe on the local side.</param>
            /// <param name="outPipeOurSide"> the output pipe on the local side.</param>
            /// <param name="pseudoConsoleHandle">the handle to the pseudoconsole.</param>
            /// <param name="processHandle">the handle to the spawned process.</param>
            /// <param name="pid">the process ID.</param>
            /// <param name="mainThreadHandle">the handle to the main thread.</param>
            public PseudoConsoleConnectionHandles(
                SafePipeHandle inPipePseudoConsoleSide,
                SafePipeHandle outPipePseudoConsoleSide,
                SafePipeHandle inPipeOurSide,
                SafePipeHandle outPipeOurSide,
                SafePseudoConsoleHandle pseudoConsoleHandle,
                SafeProcessHandle processHandle,
                int pid,
                SafeThreadHandle mainThreadHandle)
            {
                this.InPipePseudoConsoleSide = inPipePseudoConsoleSide;
                this.OutPipePseudoConsoleSide = outPipePseudoConsoleSide;
                this.InPipeOurSide = inPipeOurSide;
                this.OutPipeOurSide = outPipeOurSide;
                this.PseudoConsoleHandle = pseudoConsoleHandle;
                this.ProcessHandle = processHandle;
                this.Pid = pid;
                this.MainThreadHandle = mainThreadHandle;
            }

            /// <summary>
            /// Gets the input pipe on the pseudoconsole side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="OutPipeOurSide"/>.
            /// </remarks>
            internal SafePipeHandle InPipePseudoConsoleSide { get; }

            /// <summary>
            /// Gets the output pipe on the pseudoconsole side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="InPipeOurSide"/>.
            /// </remarks>
            internal SafePipeHandle OutPipePseudoConsoleSide { get; }

            /// <summary>
            /// Gets the input pipe on the local side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="OutPipePseudoConsoleSide"/>.
            /// </remarks>
            internal SafePipeHandle InPipeOurSide { get; }

            /// <summary>
            /// Gets the output pipe on the local side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="InPipePseudoConsoleSide"/>.
            /// </remarks>
            internal SafePipeHandle OutPipeOurSide { get; }

            /// <summary>
            /// Gets the handle to the pseudoconsole.
            /// </summary>
            internal SafePseudoConsoleHandle PseudoConsoleHandle { get; }

            /// <summary>
            /// Gets the handle to the spawned process.
            /// </summary>
            internal SafeProcessHandle ProcessHandle { get; }

            /// <summary>
            /// Gets the process ID.
            /// </summary>
            internal int Pid { get; }

            /// <summary>
            /// Gets the handle to the main thread.
            /// </summary>
            internal SafeThreadHandle MainThreadHandle { get; }
        }
    }
}
