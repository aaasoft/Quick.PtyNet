using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Pty.Net.Windows;

/// <summary>
/// A connection to a pseudoterminal spawned by native windows APIs.
/// </summary>
internal sealed class PseudoConsoleConnection : IPtyConnection, IDisposable
{
	/// <summary>
	/// handles to resources creates when a pseudoconsole is spawned.
	/// </summary>
	internal sealed class PseudoConsoleConnectionHandles
	{
		/// <summary>
		/// Gets the input pipe on the pseudoconsole side.
		/// </summary>
		/// <remarks>
		/// This pipe is connected to <see cref="P:Pty.Net.Windows.PseudoConsoleConnection.PseudoConsoleConnectionHandles.OutPipeOurSide" />.
		/// </remarks>
		internal NativeMethods.SafePipeHandle InPipePseudoConsoleSide { get; }

		/// <summary>
		/// Gets the output pipe on the pseudoconsole side.
		/// </summary>
		/// <remarks>
		/// This pipe is connected to <see cref="P:Pty.Net.Windows.PseudoConsoleConnection.PseudoConsoleConnectionHandles.InPipeOurSide" />.
		/// </remarks>
		internal NativeMethods.SafePipeHandle OutPipePseudoConsoleSide { get; }

		/// <summary>
		/// Gets the input pipe on the local side.
		/// </summary>
		/// <remarks>
		/// This pipe is connected to <see cref="P:Pty.Net.Windows.PseudoConsoleConnection.PseudoConsoleConnectionHandles.OutPipePseudoConsoleSide" />.
		/// </remarks>
		internal NativeMethods.SafePipeHandle InPipeOurSide { get; }

		/// <summary>
		/// Gets the output pipe on the local side.
		/// </summary>
		/// <remarks>
		/// This pipe is connected to <see cref="P:Pty.Net.Windows.PseudoConsoleConnection.PseudoConsoleConnectionHandles.InPipePseudoConsoleSide" />.
		/// </remarks>
		internal NativeMethods.SafePipeHandle OutPipeOurSide { get; }

		/// <summary>
		/// Gets the handle to the pseudoconsole.
		/// </summary>
		internal NativeMethods.SafePseudoConsoleHandle PseudoConsoleHandle { get; }

		/// <summary>
		/// Gets the handle to the spawned process.
		/// </summary>
		internal NativeMethods.SafeProcessHandle ProcessHandle { get; }

		/// <summary>
		/// Gets the process ID.
		/// </summary>
		internal int Pid { get; }

		/// <summary>
		/// Gets the handle to the main thread.
		/// </summary>
		internal NativeMethods.SafeThreadHandle MainThreadHandle { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Pty.Net.Windows.PseudoConsoleConnection.PseudoConsoleConnectionHandles" /> class.
		/// </summary>
		/// <param name="inPipePseudoConsoleSide">the input pipe on the pseudoconsole side.</param>
		/// <param name="outPipePseudoConsoleSide">the output pipe on the pseudoconsole side.</param>
		/// <param name="inPipeOurSide"> the input pipe on the local side.</param>
		/// <param name="outPipeOurSide"> the output pipe on the local side.</param>
		/// <param name="pseudoConsoleHandle">the handle to the pseudoconsole.</param>
		/// <param name="processHandle">the handle to the spawned process.</param>
		/// <param name="pid">the process ID.</param>
		/// <param name="mainThreadHandle">the handle to the main thread.</param>
		public PseudoConsoleConnectionHandles(NativeMethods.SafePipeHandle inPipePseudoConsoleSide, NativeMethods.SafePipeHandle outPipePseudoConsoleSide, NativeMethods.SafePipeHandle inPipeOurSide, NativeMethods.SafePipeHandle outPipeOurSide, NativeMethods.SafePseudoConsoleHandle pseudoConsoleHandle, NativeMethods.SafeProcessHandle processHandle, int pid, NativeMethods.SafeThreadHandle mainThreadHandle)
		{
			InPipePseudoConsoleSide = inPipePseudoConsoleSide;
			OutPipePseudoConsoleSide = outPipePseudoConsoleSide;
			InPipeOurSide = inPipeOurSide;
			OutPipeOurSide = outPipeOurSide;
			PseudoConsoleHandle = pseudoConsoleHandle;
			ProcessHandle = processHandle;
			Pid = pid;
			MainThreadHandle = mainThreadHandle;
		}
	}

	private readonly Process process;

	private PseudoConsoleConnectionHandles handles;

	/// <inheritdoc />
	public Stream ReaderStream { get; }

	/// <inheritdoc />
	public Stream WriterStream { get; }

	/// <inheritdoc />
	public int Pid => handles.Pid;

	/// <inheritdoc />
	public int ExitCode => process.ExitCode;

	/// <inheritdoc />
	public event EventHandler<PtyExitedEventArgs> ProcessExited;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Windows.PseudoConsoleConnection" /> class.
	/// </summary>
	/// <param name="handles">The set of handles associated with the pseudoconsole.</param>
	public PseudoConsoleConnection(PseudoConsoleConnectionHandles handles)
	{
		ReaderStream = new AnonymousPipeClientStream(PipeDirection.In, new SafePipeHandle(handles.OutPipeOurSide.Handle, ownsHandle: false));
		WriterStream = new AnonymousPipeClientStream(PipeDirection.Out, new SafePipeHandle(handles.InPipeOurSide.Handle, ownsHandle: false));
		this.handles = handles;
		process = Process.GetProcessById(Pid);
		process.Exited += Process_Exited;
		process.EnableRaisingEvents = true;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		ReaderStream?.Dispose();
		WriterStream?.Dispose();
		if (handles != null)
		{
			handles.PseudoConsoleHandle.Close();
			handles.MainThreadHandle.Close();
			handles.ProcessHandle.Close();
			handles.InPipeOurSide.Close();
			handles.InPipePseudoConsoleSide.Close();
			handles.OutPipePseudoConsoleSide.Close();
			handles.OutPipeOurSide.Close();
		}
	}

	/// <inheritdoc />
	public void Kill()
	{
		process.Kill();
	}

	/// <inheritdoc />
	public void Resize(int cols, int rows)
	{
		int num = NativeMethods.ResizePseudoConsole(handles.PseudoConsoleHandle, new NativeMethods.Coord(cols, rows));
		if (num != 0)
		{
			Marshal.ThrowExceptionForHR(num);
		}
	}

	/// <inheritdoc />
	public bool WaitForExit(int milliseconds)
	{
		return process.WaitForExit(milliseconds);
	}

	private void Process_Exited(object sender, EventArgs e)
	{
		this.ProcessExited?.Invoke(this, new PtyExitedEventArgs(process.ExitCode));
	}
}
