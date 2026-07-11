using System;
using System.Threading.Tasks;
using Packet.Logging;

namespace Packet.Extensions;

internal static class TaskExtensions
{
    internal static void Forget(this Task task, string operationName)
    {
        if (task.IsCompleted)
        {
            ObserveCompletedTask(task, operationName);
            return;
        }

        _ = ObserveAsync(task, operationName);
    }

    static void ObserveCompletedTask(Task task, string operationName)
    {
        if (task.IsCanceled || task.Exception == null)
        {
            return;
        }

        PacketLog.Error($"{operationName} failed: {task.Exception.GetBaseException().Message}");
    }

    static async Task ObserveAsync(Task task, string operationName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            PacketLog.Error($"{operationName} failed: {ex.Message}");
        }
    }
}
