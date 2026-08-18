using Jobs.Domain.Common;
using JobTracker.SharedKernel.Primitives;
using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Customers;

public sealed class Customer : AggregateRoot<CustomerId>
{
    public OrganizationId OrganizationId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Customer() { }

    public static Result<Customer> Create(
        OrganizationId organizationId,
        string? name,
        string? email,
        string? phone,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return CustomerErrors.InvalidName;
        if (email is { Length: > 200 })
            return CustomerErrors.EmailTooLong;
        if (phone is { Length: > 40 })
            return CustomerErrors.PhoneTooLong;

        var trimmed = name!.Trim();
        return new Customer
        {
            Id = new CustomerId(Guid.NewGuid()),
            OrganizationId = organizationId,
            Name = trimmed,
            NameNormalized = trimmed.ToLowerInvariant(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email!.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone!.Trim(),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };
    }

    public Result Rename(string? name, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure(CustomerErrors.InvalidName);
        Name = name!.Trim();
        NameNormalized = Name.ToLowerInvariant();
        UpdatedAt = nowUtc;
        return Result.Success();
    }
}
