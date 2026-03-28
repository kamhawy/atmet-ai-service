using ATMET.AI.Core.Models.Portal;
using FluentValidation;

namespace ATMET.AI.Infrastructure.Validators;

public class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("Service ID is required");
        RuleFor(x => x.EntityId).NotEmpty().WithMessage("Entity ID is required");
    }
}

public class UpdateCaseStatusRequestValidator : AbstractValidator<UpdateCaseStatusRequest>
{
    public UpdateCaseStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required");
    }
}

public class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty().WithMessage("Entity ID is required");
    }
}

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Message content is required");
    }
}

public class UpdateFormDataRequestValidator : AbstractValidator<UpdateFormDataRequest>
{
    public UpdateFormDataRequestValidator()
    {
        RuleFor(x => x.FormData).Must(x => x.ValueKind == System.Text.Json.JsonValueKind.Object)
            .WithMessage("Form data must be a JSON object");
    }
}

public class ValidateFormRequestValidator : AbstractValidator<ValidateFormRequest>
{
    public ValidateFormRequestValidator()
    {
        RuleFor(x => x.FormData).Must(x => x.ValueKind == System.Text.Json.JsonValueKind.Object)
            .WithMessage("Form data must be a JSON object");
    }
}
