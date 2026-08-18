using JobTracker.SharedKernel.Results;

namespace JobTracker.Tests.Unit.SharedKernel;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_exposes_value_and_no_error()
    {
        Result<int> r = 42;
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
        r.Error.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Failure_result_exposes_error_and_default_value()
    {
        Result<int> r = Error.NotFound("Test.NotFound", "nope");
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Test.NotFound");
        r.Value.Should().Be(0);
    }

    [Fact]
    public void Result_generic_success_stores_the_value()
    {
        var r = Result.Success("hello");
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("hello");
    }
}
