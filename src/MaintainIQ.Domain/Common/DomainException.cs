namespace MaintainIQ.Domain.Common;

public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message)
        : base(message)
    {
    }
}

public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string ruleCode, string message)
        : base(message)
    {
        RuleCode = ruleCode;
    }

    public string RuleCode { get; }
}

public sealed class PermissionDeniedException : DomainException
{
    public PermissionDeniedException(string message)
        : base(message)
    {
    }
}
