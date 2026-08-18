using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Employees;

public static class EmployeeErrors
{
    public static readonly Error InvalidName =
        Error.Validation("Employee.InvalidName", "Name must be non-empty and at most 200 characters.");

    public static readonly Error EmailTooLong =
        Error.Validation("Employee.EmailTooLong", "Email must be at most 200 characters.");

    public static readonly Error PhoneTooLong =
        Error.Validation("Employee.PhoneTooLong", "Phone must be at most 40 characters.");

    public static readonly Error NameAlreadyExists =
        Error.Conflict("Employee.NameAlreadyExists", "An employee with this name already exists in the organization.");
}
