using System.Collections.Concurrent;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Services;

public sealed class PendingConfirmationStore
{
    private readonly ConcurrentDictionary<Guid, PendingConfirmation> _items = new();

    public void Add(Guid id, PendingConfirmation item) => _items[id] = item;
    public bool TryGet(Guid id, out PendingConfirmation? item) => _items.TryGetValue(id, out item);
    public bool TryRemove(Guid id, out PendingConfirmation? item) => _items.TryRemove(id, out item);
}

public record PendingConfirmation(
    AgentRequest OriginalRequest,
    AgentResult AgentResult,
    string AgentName);
