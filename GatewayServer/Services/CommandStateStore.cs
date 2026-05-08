using System.Collections.Concurrent;
using GatewayServer.Models;

namespace GatewayServer.Services;

public sealed class CommandStateStore
{
    private readonly ConcurrentDictionary<string, FrontendCommandContext> _pending = new(StringComparer.OrdinalIgnoreCase);

    public void Save(FrontendCommandContext context)
    {
        _pending[context.CommandId] = context;
    }

    public bool TryGet(string commandId, out FrontendCommandContext? context)
    {
        if (_pending.TryGetValue(commandId, out var found))
        {
            context = found;
            return true;
        }

        context = null;
        return false;
    }

    public bool Remove(string commandId)
    {
        return _pending.TryRemove(commandId, out _);
    }
}