using ATMET.AI.Core.Models.Foundry;

namespace ATMET.AI.Core.Services;

/// <summary>
/// Supabase-backed reads for **internal** Foundry agent HTTP tools (entity-scoped, no portal user header).
/// </summary>
public interface IFoundryAgentReadService
{
    Task<CaseDetailForAgent?> GetCaseForAgentAsync(string caseId, string entityId, CancellationToken ct = default);

    Task<CaseDetailForAgent?> GetCaseByReferenceForAgentAsync(string referenceNumber, string entityId,
        CancellationToken ct = default);

    Task<ServiceDetailForAgent?> GetServiceForAgentAsync(string serviceId, string entityId,
        CancellationToken ct = default);
}
