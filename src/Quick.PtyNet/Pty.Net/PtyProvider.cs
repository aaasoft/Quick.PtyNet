using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Pty.Net;

/// <summary>
/// Provides the ability to spawn new processes under a pseudoterminal.
/// </summary>
public static class PtyProvider
{
	private static readonly TraceSource Trace = new TraceSource("PtyProvider");

	/// <summary>
	/// Spawn a new process connected to a pseudoterminal.
	/// </summary>
	/// <param name="options">The set of options for creating the pseudoterminal.</param>
	/// <param name="cancellationToken">The token to cancel process creation early.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that completes once the process has spawned.</returns>
	public static Task<IPtyConnection> SpawnAsync(PtyOptions options, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(options.App))
		{
			throw new ArgumentNullException("App");
		}
		if (string.IsNullOrEmpty(options.Cwd))
		{
			throw new ArgumentNullException("Cwd");
		}
		if (options.CommandLine == null)
		{
			throw new ArgumentNullException("CommandLine");
		}
		if (options.Environment == null)
		{
			throw new ArgumentNullException("Environment");
		}
		IDictionary<string, string> environment = MergeEnvironment(PlatformServices.PtyEnvironment, null);
		environment = MergeEnvironment(options.Environment, environment);
		options.Environment = environment;
		return PlatformServices.PtyProvider.StartTerminalAsync(options, Trace, cancellationToken);
	}

	private static IDictionary<string, string> MergeEnvironment(IDictionary<string, string> enviromentToMerge, IDictionary<string, string> environment)
	{
		if (environment == null)
		{
			environment = new Dictionary<string, string>(PlatformServices.EnvironmentVariableComparer);
			foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
			{
				environment[environmentVariable.Key.ToString()] = environmentVariable.Value.ToString();
			}
		}
		foreach (KeyValuePair<string, string> item in enviromentToMerge)
		{
			if (string.IsNullOrEmpty(item.Value))
			{
				environment.Remove(item.Key);
			}
			else
			{
				environment[item.Key] = item.Value;
			}
		}
		return environment;
	}
}
