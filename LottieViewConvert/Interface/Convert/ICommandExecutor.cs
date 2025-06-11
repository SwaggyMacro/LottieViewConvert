using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LottieViewConvert.Interface.Convert
{
    /// <summary>
    /// Interface for executing external commands.
    /// </summary>
    public interface ICommandExecutor
    {
        Task<bool> ExecuteAsync(
            string command,
            IEnumerable<string> arguments,
            string workingDirectory,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default);
    }
}