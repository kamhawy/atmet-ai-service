using ATMET.AI.Core.Models.Foundry;
using ATMET.AI.Core.Services;

namespace ATMET.AI.Api.Tests.Fakes;

/// <summary>
/// Deterministic stub for internal Foundry HTTP route integration tests (no Supabase).
/// </summary>
public sealed class FakeFoundryAgentReadService : IFoundryAgentReadService
{
    public const string ValidEntityId = "11111111-1111-1111-1111-111111111111";
    public const string ValidCaseId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    public const string ValidReference = "REF-ATMET-TEST";
    public const string ValidServiceId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    public Task<CaseDetailForAgent?> GetCaseForAgentAsync(string caseId, string entityId, CancellationToken ct = default)
    {
        if (caseId == ValidCaseId && entityId == ValidEntityId)
            return Task.FromResult<CaseDetailForAgent?>(MinimalCase());

        return Task.FromResult<CaseDetailForAgent?>(null);
    }

    public Task<CaseDetailForAgent?> GetCaseByReferenceForAgentAsync(string referenceNumber, string entityId,
        CancellationToken ct = default)
    {
        if (referenceNumber == ValidReference && entityId == ValidEntityId)
            return Task.FromResult<CaseDetailForAgent?>(MinimalCase());

        return Task.FromResult<CaseDetailForAgent?>(null);
    }

    public Task<ServiceDetailForAgent?> GetServiceForAgentAsync(string serviceId, string entityId,
        CancellationToken ct = default)
    {
        if (serviceId == ValidServiceId && entityId == ValidEntityId)
        {
            return Task.FromResult<ServiceDetailForAgent?>(new ServiceDetailForAgent(
                Id: ValidServiceId,
                Name: "Test service",
                NameAr: null,
                Description: null,
                DescriptionAr: null,
                Category: "general",
                SlaDays: 5,
                IsActive: true,
                EntityId: ValidEntityId,
                FormSchema: null,
                RequiredDocuments: null,
                Intents: null,
                Workflow: null));
        }

        return Task.FromResult<ServiceDetailForAgent?>(null);
    }

    private static CaseDetailForAgent MinimalCase() => new(
        Id: ValidCaseId,
        ReferenceNumber: ValidReference,
        Status: "submitted",
        CurrentStep: null,
        ServiceId: ValidServiceId,
        ServiceName: "Test service",
        ServiceNameAr: null,
        ServiceCategory: "general",
        EntityId: ValidEntityId,
        RequesterUserId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
        WorkflowVersionId: null,
        SubmittedData: null,
        EligibilityResult: null,
        WorkflowState: null,
        Conversations: [],
        CreatedAt: DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
        UpdatedAt: DateTimeOffset.Parse("2025-01-02T00:00:00Z"));
}
