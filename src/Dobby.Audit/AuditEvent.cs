using Dobby.Core;
using System.Globalization;
using System.Text.Json;

namespace Dobby.Audit;

public sealed record AuditEvent(
    string EventName,
    string ActorId,
    string Target,
    AuditResult Result,
    CorrelationId CorrelationId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Details = null)
{
    public IReadOnlyDictionary<string, string> ToCanonicalPayload()
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["timestamp"] = OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["actor"] = ActorId,
            ["action"] = EventName,
            ["target"] = Target,
            ["result"] = Result.ToPayloadValue(),
            ["correlationId"] = CorrelationId.ToString()
        };

        if (Details is not null)
        {
            var orderedDetails = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var detail in Details)
            {
                orderedDetails[detail.Key] = detail.Value;
            }

            payload["details"] = JsonSerializer.Serialize(orderedDetails);
        }

        return payload;
    }
}
