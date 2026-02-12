// ====================================================================================
// FluentValidation Validators
// ====================================================================================

namespace ATMET.AI.Infrastructure.Validators;

using ATMET.AI.Core.Models.Requests;
using FluentValidation;

public class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Model).NotEmpty().WithMessage("Model deployment name is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256).WithMessage("Agent name is required and must be ≤256 characters");
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.TopP).InclusiveBetween(0f, 1f).When(x => x.TopP.HasValue);
    }
}

public class CreateMessageRequestValidator : AbstractValidator<CreateMessageRequest>
{
    public CreateMessageRequestValidator()
    {
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "user" or "assistant")
            .WithMessage("Role must be 'user' or 'assistant'");
        RuleFor(x => x.Content).NotEmpty().WithMessage("Message content is required");
    }
}

public class CreateRunRequestValidator : AbstractValidator<CreateRunRequest>
{
    public CreateRunRequestValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty().WithMessage("AgentId is required");
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.TopP).InclusiveBetween(0f, 1f).When(x => x.TopP.HasValue);
        RuleFor(x => x.MaxPromptTokens).GreaterThan(0).When(x => x.MaxPromptTokens.HasValue);
        RuleFor(x => x.MaxCompletionTokens).GreaterThan(0).When(x => x.MaxCompletionTokens.HasValue);
    }
}

public class ChatCompletionRequestValidator : AbstractValidator<ChatCompletionRequest>
{
    public ChatCompletionRequestValidator()
    {
        RuleFor(x => x.Messages).NotEmpty().WithMessage("At least one message is required");
        RuleForEach(x => x.Messages).ChildRules(msg =>
        {
            msg.RuleFor(m => m.Role).NotEmpty();
            msg.RuleFor(m => m.Content).NotEmpty();
        });
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0)
            .When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).GreaterThan(0)
            .When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.TopP).InclusiveBetween(0.0, 1.0)
            .When(x => x.TopP.HasValue);
    }
}

public class CreateIndexRequestValidator : AbstractValidator<CreateIndexRequest>
{
    public CreateIndexRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Index name is required");
        RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required");
        RuleFor(x => x.ConnectionName).NotEmpty().WithMessage("Connection name is required");
        RuleFor(x => x.IndexName).NotEmpty().WithMessage("Index name is required");
    }
}
