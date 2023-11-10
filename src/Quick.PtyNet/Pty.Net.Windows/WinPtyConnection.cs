using System;
using System.Diagnostics;
using System.IO;

namespace Pty.Net.Windows;

/// <summary>
/// A connection to a pseudoterminal spawned via winpty.
/// </summary>
internal class WinPtyConnection : IPtyConnection, IDisposable
{
	private readonly IntPtr handle;

	private readonly NativeMethods.SafeProcessHandle processHandle;

	private readonly Process process;

	/// <inheritdoc />
	public Stream ReaderStream { get; }

	/// <inheritdoc />
	public Stream WriterStream { get; }

	/// <inheritdoc />
	public int Pid { get; }

	/// <inheritdoc />
	public int ExitCode => process.ExitCode;

	/// <inheritdoc />
	public event EventHandler<PtyExitedEventArgs> ProcessExited;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Windows.WinPtyConnection" /> class.
	/// </summary>
	/// <param name="readerStream">The reading side of the pty connection.</param>
	/// <param name="writerStream">The writing side of the pty connection.</param>
	/// <param name="handle">A handle to the winpty instance.</param>
	/// <param name="processHandle">A handle to the spawned process.</param>
	public WinPtyConnection(Stream readerStream, Stream writerStream, IntPtr handle, NativeMethods.SafeProcessHandle processHandle)
	{
		ReaderStream = readerStream;
		WriterStream = writerStream;
		Pid = NativeMethods.GetProcessId(processHandle);
		this.handle = handle;
		this.processHandle = processHandle;
		process = Process.GetProcessById(Pid);
		process.Exited += Process_Exited;
		process.EnableRaisingEvents = true;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		ReaderStream?.Dispose();
		WriterStream?.Dispose();
		processHandle.Close();
		WinptyNativeInterop.winpty_free(handle);
	}

	/// <inheritdoc />
	public void Kill()
	{
		process.Kill();
	}

	/// <inheritdoc />
	public void Resize(int cols, int rows)
	{
		WinptyNativeInterop.winpty_set_size(handle, cols, rows, out var _);
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
