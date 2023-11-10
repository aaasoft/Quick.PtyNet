using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Pty.Net.Linux;
using Pty.Net.Mac;
using Pty.Net.Windows;

namespace Pty.Net;

/// <summary>
/// Provides platform specific functionality.
/// </summary>
internal static class PlatformServices
{
	private static readonly Lazy<IPtyProvider> WindowsProviderLazy;

	private static readonly Lazy<IPtyProvider> LinuxProviderLazy;

	private static readonly Lazy<IPtyProvider> MacProviderLazy;

	private static readonly Lazy<IPtyProvider> PtyProviderLazy;

	private static readonly IDictionary<string, string> WindowsPtyEnvironment;

	private static readonly IDictionary<string, string> UnixPtyEnvironment;

	/// <summary>
	/// Gets the <see cref="T:Pty.Net.IPtyProvider" /> for the current platform.
	/// </summary>
	public static IPtyProvider PtyProvider => PtyProviderLazy.Value;

	/// <summary>
	/// Gets the comparer to determine if two environment variable keys are equivalent on the current platform.
	/// </summary>
	public static StringComparer EnvironmentVariableComparer { get; }

	/// <summary>
	/// Gets specific environment variables that are needed when spawning the PTY.
	/// </summary>
	public static IDictionary<string, string> PtyEnvironment { get; }

	private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

	private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	static PlatformServices()
	{
		WindowsProviderLazy = new Lazy<IPtyProvider>(() => new Pty.Net.Windows.PtyProvider());
		LinuxProviderLazy = new Lazy<IPtyProvider>(() => new Pty.Net.Linux.PtyProvider());
		MacProviderLazy = new Lazy<IPtyProvider>(() => new Pty.Net.Mac.PtyProvider());
		WindowsPtyEnvironment = new Dictionary<string, string>();
		UnixPtyEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "TERM", "xterm-256color" },
			{
				"TMUX",
				string.Empty
			},
			{
				"TMUX_PANE",
				string.Empty
			},
			{
				"STY",
				string.Empty
			},
			{
				"WINDOW",
				string.Empty
			},
			{
				"WINDOWID",
				string.Empty
			},
			{
				"TERMCAP",
				string.Empty
			},
			{
				"COLUMNS",
				string.Empty
			},
			{
				"LINES",
				string.Empty
			}
		};
		if (IsWindows)
		{
			PtyProviderLazy = WindowsProviderLazy;
			EnvironmentVariableComparer = StringComparer.OrdinalIgnoreCase;
			PtyEnvironment = WindowsPtyEnvironment;
			return;
		}
		if (IsMac)
		{
			PtyProviderLazy = MacProviderLazy;
			EnvironmentVariableComparer = StringComparer.Ordinal;
			PtyEnvironment = UnixPtyEnvironment;
			return;
		}
		if (IsLinux)
		{
			PtyProviderLazy = LinuxProviderLazy;
			EnvironmentVariableComparer = StringComparer.Ordinal;
			PtyEnvironment = UnixPtyEnvironment;
			return;
		}
		throw new PlatformNotSupportedException();
	}
}
