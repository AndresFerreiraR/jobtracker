using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Customers;

public static class CustomerErrors
{
    public static readonly Error InvalidName =
        Error.Validation("Customer.InvalidName", "Name must be non-empty and at most 200 characters.");

    public static readonly Error EmailTooLong =
        Error.Validation("Customer.EmailTooLong", "Email must be at most 200 characters.");

    public static readonly Error PhoneTooLong =
        Error.Validation("Customer.PhoneTooLong", "Phone must be at most 40 characters.");

    public static readonly Error NameAlreadyExists =
        Error.Conflict("Customer.NameAlreadyExists", "A customer with this name already exists in the organization.");
}
