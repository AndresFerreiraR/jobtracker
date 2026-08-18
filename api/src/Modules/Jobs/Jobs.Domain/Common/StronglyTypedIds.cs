namespace Jobs.Domain.Common;

public readonly record struct JobId(Guid Value)
{
    public static JobId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct JobPhotoId(Guid Value)
{
    public static JobPhotoId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct OrganizationId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct AssigneeId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct EmployeeId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
