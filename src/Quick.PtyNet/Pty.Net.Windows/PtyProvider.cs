#define TRACE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pty.Net.Windows;

/// <summary>
/// Provides a pty connection for windows machines.
/// </summary>
internal class PtyProvider : IPtyProvider
{
	/// <inheritdoc />
	public Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken)
	{
		if (NativeMethods.IsPseudoConsoleSupported && !options.ForceWinPty)
		{
			return StartPseudoConsoleAsync(options, trace, cancellationToken);
		}
		return StartWinPtyTerminalAsync(options, trace, cancellationToken);
	}

	private static void ThrowIfErrorOrNull(string message, IntPtr err, IntPtr ptr)
	{
		ThrowIfError(message, err);
		if (ptr == IntPtr.Zero)
		{
			throw new InvalidOperationException(message + ": unexpected null result");
		}
	}

	private static void ThrowIfError(string message, IntPtr error, bool alwaysThrow = false)
	{
		if (error != IntPtr.Zero)
		{
			string message2 = $"{message}: {WinptyNativeInterop.winpty_error_msg(error)} ({WinptyNativeInterop.winpty_error_code(error)})";
			WinptyNativeInterop.winpty_error_free(error);
			throw new InvalidOperationException(message2);
		}
		if (alwaysThrow)
		{
			throw new InvalidOperationException(message);
		}
	}

	private static async Task<Stream> CreatePipeAsync(string pipeName, PipeDirection direction, CancellationToken cancellationToken)
	{
		string serverName = ".";
		if (pipeName.StartsWith("\\"))
		{
			int num = pipeName.IndexOf('\\', 2);
			if (num != -1)
			{
				serverName = pipeName.Substring(2, num - 2);
			}
			int num2 = pipeName.IndexOf('\\', num + 1);
			if (num2 != -1)
			{
				pipeName = pipeName.Substring(num2 + 1);
			}
		}
		NamedPipeClientStream pipe = new NamedPipeClientStream(serverName, pipeName, direction);
		await pipe.ConnectAsync(cancellationToken);
		return pipe;
	}

	private static string GetAppOnPath(string app, string cwd, IDictionary<string, string> env)
	{
		bool flag = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null;
		string environmentVariable = Environment.GetEnvironmentVariable("WINDIR");
		string sysnativePath = Path.Combine(environmentVariable, "Sysnative");
		string text = sysnativePath;
		char directorySeparatorChar = Path.DirectorySeparatorChar;
		string sysnativePathWithSlash = text + directorySeparatorChar;
		string system32Path = Path.Combine(environmentVariable, "System32");
		string text2 = system32Path;
		directorySeparatorChar = Path.DirectorySeparatorChar;
		string system32PathWithSlash = text2 + directorySeparatorChar;
		try
		{
			if (Path.IsPathRooted(app))
			{
				if (flag)
				{
					if (app.StartsWith(system32PathWithSlash, StringComparison.OrdinalIgnoreCase))
					{
						string text3 = Path.Combine(sysnativePath, app.Substring(system32PathWithSlash.Length));
						if (File.Exists(text3))
						{
							return text3;
						}
					}
				}
				else if (app.StartsWith(sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase))
				{
					return Path.Combine(system32Path, app.Substring(sysnativePathWithSlash.Length));
				}
				return app;
			}
			if (Path.GetDirectoryName(app) != string.Empty)
			{
				return Path.Combine(cwd, app);
			}
		}
		catch (ArgumentException)
		{
			throw new ArgumentException("Invalid terminal app path '" + app + "'");
		}
		catch (PathTooLongException)
		{
			throw new ArgumentException("Terminal app path '" + app + "' is too long");
		}
		string value;
		string text4 = ((env != null && env.TryGetValue("PATH", out value)) ? value : null) ?? Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrWhiteSpace(text4))
		{
			return Path.Combine(cwd, app);
		}
		List<string> list = new List<string>(text4.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries));
		if (flag)
		{
			int num = list.FindIndex((string entry) => string.Equals(entry, system32Path, StringComparison.OrdinalIgnoreCase) || string.Equals(entry, system32PathWithSlash, StringComparison.OrdinalIgnoreCase));
			int num2 = list.FindIndex((string entry) => string.Equals(entry, sysnativePath, StringComparison.OrdinalIgnoreCase) || string.Equals(entry, sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase));
			if (num >= 0 && num2 == -1)
			{
				list.Insert(num, sysnativePath);
			}
		}
		foreach (string item in list)
		{
			bool flag2;
			try
			{
				flag2 = Path.IsPathRooted(item);
			}
			catch (ArgumentException)
			{
				continue;
			}
			string text5 = (flag2 ? Path.Combine(item, app) : Path.Combine(cwd, item, app));
			if (File.Exists(text5))
			{
				return text5;
			}
			string text6 = text5 + ".com";
			if (File.Exists(text6))
			{
				return text6;
			}
			text6 = text5 + ".exe";
			if (File.Exists(text6))
			{
				return text6;
			}
		}
		return Path.Combine(cwd, app);
	}

	private static string GetEnvironmentString(IDictionary<string, string> environment)
	{
		string[] array = new string[environment.Count];
		environment.Keys.CopyTo(array, 0);
		string[] array2 = new string[environment.Count];
		environment.Values.CopyTo(array2, 0);
		Array.Sort(array, array2, (IComparer<string>)StringComparer.OrdinalIgnoreCase);
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < environment.Count; i++)
		{
			stringBuilder.Append(array[i]);
			stringBuilder.Append('=');
			stringBuilder.Append(array2[i]);
			stringBuilder.Append('\0');
		}
		stringBuilder.Append('\0');
		return stringBuilder.ToString();
	}

	private async Task<IPtyConnection> StartWinPtyTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken)
	{
		IntPtr err;
		IntPtr intPtr = WinptyNativeInterop.winpty_config_new(4uL, out err);
		ThrowIfErrorOrNull("Error creating WinPTY config", err, intPtr);
		WinptyNativeInterop.winpty_config_set_initial_size(intPtr, options.Cols, options.Rows);
		IntPtr handle = WinptyNativeInterop.winpty_open(intPtr, out err);
		WinptyNativeInterop.winpty_config_free(intPtr);
		ThrowIfErrorOrNull("Error launching WinPTY agent", err, handle);
		string text = (options.VerbatimCommandLine ? WindowsArguments.FormatVerbatim(options.CommandLine) : WindowsArguments.Format(options.CommandLine));
		string environmentString = GetEnvironmentString(options.Environment);
		string appOnPath = GetAppOnPath(options.App, options.Cwd, options.Environment);
		trace.TraceInformation("Starting terminal process '" + appOnPath + "' with command line " + text);
		IntPtr intPtr2 = WinptyNativeInterop.winpty_spawn_config_new(1uL, appOnPath, text, options.Cwd, environmentString, out err);
		ThrowIfErrorOrNull("Error creating WinPTY spawn config", err, intPtr2);
		NativeMethods.SafeProcessHandle hProcess;
		IntPtr thread_handle;
		int create_process_error;
		bool num = WinptyNativeInterop.winpty_spawn(handle, intPtr2, out hProcess, out thread_handle, out create_process_error, out err);
		WinptyNativeInterop.winpty_spawn_config_free(intPtr2);
		if (!num)
		{
			if (create_process_error != 0)
			{
				if (err != IntPtr.Zero)
				{
					WinptyNativeInterop.winpty_error_free(err);
				}
				throw new InvalidOperationException($"Unable to start WinPTY terminal '{appOnPath}': {new Win32Exception(create_process_error).Message} ({create_process_error})");
			}
			ThrowIfError("Unable to start WinPTY terminal process", err, alwaysThrow: true);
		}
		Stream writeToStream = null;
		Stream readerStream;
		try
		{
			writeToStream = await CreatePipeAsync(WinptyNativeInterop.winpty_conin_name(handle), PipeDirection.Out, cancellationToken);
			readerStream = await CreatePipeAsync(WinptyNativeInterop.winpty_conout_name(handle), PipeDirection.In, cancellationToken);
		}
		catch
		{
			writeToStream?.Dispose();
			hProcess.Close();
			WinptyNativeInterop.winpty_free(handle);
			throw;
		}
		return new WinPtyConnection(readerStream, writeToStream, handle, hProcess);
	}

	private Task<IPtyConnection> StartPseudoConsoleAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken)
	{
		if (!NativeMethods.CreatePipe(out NativeMethods.SafePipeHandle hReadPipe, out NativeMethods.SafePipeHandle hWritePipe, null, 0))
		{
			throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
		}
		if (!NativeMethods.CreatePipe(out NativeMethods.SafePipeHandle hReadPipe2, out NativeMethods.SafePipeHandle hWritePipe2, null, 0))
		{
			throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
		}
		NativeMethods.Coord coord = new NativeMethods.Coord(options.Cols, options.Rows);
		NativeMethods.SafePseudoConsoleHandle safePseudoConsoleHandle = new NativeMethods.SafePseudoConsoleHandle();
		RuntimeHelpers.PrepareConstrainedRegions();
		int num;
		try
		{
		}
		finally
		{
			num = NativeMethods.CreatePseudoConsole(coord, hReadPipe.Handle, hWritePipe2.Handle, 0u, out var consoleHandle);
			if (consoleHandle != IntPtr.Zero && consoleHandle != NativeMethods.INVALID_HANDLE_VALUE)
			{
				safePseudoConsoleHandle.InitialSetHandle(consoleHandle);
			}
		}
		if (num != 0)
		{
			Marshal.ThrowExceptionForHR(num);
		}
		NativeMethods.STARTUPINFOEX lpStartupInfo = default(NativeMethods.STARTUPINFOEX);
		lpStartupInfo.InitAttributeListAttachedToConPTY(safePseudoConsoleHandle);
		IntPtr intPtr = Marshal.StringToHGlobalUni(GetEnvironmentString(options.Environment));
		try
		{
			string appOnPath = GetAppOnPath(options.App, options.Cwd, options.Environment);
			string text = (options.VerbatimCommandLine ? WindowsArguments.FormatVerbatim(options.CommandLine) : WindowsArguments.Format(options.CommandLine));
			StringBuilder stringBuilder = new StringBuilder(appOnPath.Length + text.Length + 4);
			if (appOnPath.Contains(" ") && !appOnPath.StartsWith("\"") && !appOnPath.EndsWith("\""))
			{
				stringBuilder.Append('"').Append(appOnPath).Append('"');
			}
			else
			{
				stringBuilder.Append(appOnPath);
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				stringBuilder.Append(' ');
				stringBuilder.Append(text);
			}
			int error = 0;
			NativeMethods.PROCESS_INFORMATION lpProcessInformation = default(NativeMethods.PROCESS_INFORMATION);
			NativeMethods.SafeProcessHandle safeProcessHandle = new NativeMethods.SafeProcessHandle();
			NativeMethods.SafeThreadHandle safeThreadHandle = new NativeMethods.SafeThreadHandle();
			RuntimeHelpers.PrepareConstrainedRegions();
			bool flag;
			try
			{
			}
			finally
			{
				flag = NativeMethods.CreateProcess(null, stringBuilder.ToString(), null, null, bInheritHandles: false, 525312, intPtr, options.Cwd, ref lpStartupInfo, out lpProcessInformation);
				if (!flag)
				{
					error = Marshal.GetLastWin32Error();
				}
				if (lpProcessInformation.hProcess != IntPtr.Zero && lpProcessInformation.hProcess != NativeMethods.INVALID_HANDLE_VALUE)
				{
					safeProcessHandle.InitialSetHandle(lpProcessInformation.hProcess);
				}
				if (lpProcessInformation.hThread != IntPtr.Zero && lpProcessInformation.hThread != NativeMethods.INVALID_HANDLE_VALUE)
				{
					safeThreadHandle.InitialSetHandle(lpProcessInformation.hThread);
				}
			}
			if (!flag)
			{
				Win32Exception ex = new Win32Exception(error);
				throw new InvalidOperationException("Could not start terminal process " + stringBuilder.ToString() + ": " + ex.Message, ex);
			}
			return Task.FromResult((IPtyConnection)new PseudoConsoleConnection(new PseudoConsoleConnection.PseudoConsoleConnectionHandles(hReadPipe, hWritePipe2, hWritePipe, hReadPipe2, safePseudoConsoleHandle, safeProcessHandle, lpProcessInformation.dwProcessId, safeThreadHandle)));
		}
		finally
		{
			lpStartupInfo.FreeAttributeList();
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
	}
}
