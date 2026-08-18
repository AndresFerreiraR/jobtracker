using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using Jobs.Domain.Jobs.Events;

namespace JobTracker.Tests.Unit.Jobs.Domain;

public sealed class JobTests
{
    private static readonly OrganizationId OrgId = new(Guid.NewGuid());
    private static readonly CustomerId CustomerId = new(Guid.NewGuid());
    private static readonly AssigneeId AssigneeId = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly Address ValidAddress =
        Address.Create("123 Main", "Miami", "FL", "33101").Value!;

    private static Job NewDraftJob() =>
        Job.Create(OrgId, "Roof repair", "Replace shingles", ValidAddress, CustomerId, Now).Value!;

    [Fact]
    public void Create_persists_all_fields_and_raises_JobCreated()
    {
        var result = Job.Create(OrgId, "Roof repair", "desc", ValidAddress, CustomerId, Now);

        result.IsSuccess.Should().BeTrue();
        var job = result.Value!;
        job.Status.Should().Be(JobStatus.Draft);
        job.OrganizationId.Should().Be(OrgId);
        job.CustomerId.Should().Be(CustomerId);
        job.CreatedAt.Should().Be(Now);
        job.Events.Should().ContainSingle(e => e is JobCreatedDomainEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_title(string title)
    {
        var r = Job.Create(OrgId, title, "d", ValidAddress, CustomerId, Now);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Job.InvalidTitle");
    }

    [Fact]
    public void Create_rejects_title_longer_than_200()
    {
        var r = Job.Create(OrgId, new string('a', 201), "d", ValidAddress, CustomerId, Now);
        r.Error.Code.Should().Be("Job.InvalidTitle");
    }

    [Fact]
    public void Schedule_from_Draft_transitions_to_Scheduled_and_raises_event()
    {
        var job = NewDraftJob();

        var r = job.Schedule(Now.AddDays(1), AssigneeId, Now);

        r.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Scheduled);
        job.AssigneeId.Should().Be(AssigneeId);
        job.Events.Should().Contain(e => e is JobScheduledDomainEvent);
    }

    [Fact]
    public void Schedule_in_the_past_is_rejected()
    {
        var job = NewDraftJob();
        var r = job.Schedule(Now.AddDays(-1), AssigneeId, Now);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Job.CannotScheduleInPast");
    }

    [Fact]
    public void Cannot_start_a_Draft_job()
    {
        var job = NewDraftJob();
        var r = job.Start(Now);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Job.InvalidTransition");
    }

    [Fact]
    public void Full_happy_path_Draft_to_Completed()
    {
        var job = NewDraftJob();
        job.Schedule(Now.AddDays(1), AssigneeId, Now).IsSuccess.Should().BeTrue();
        job.Start(Now.AddDays(1).AddHours(9)).IsSuccess.Should().BeTrue();
        job.AddPhoto("https://cdn/photo.jpg", Now.AddDays(1).AddHours(10), null)
            .IsSuccess.Should().BeTrue();
        var completed = job.Complete("https://cdn/sig.png", Now.AddDays(1).AddHours(11));

        completed.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Completed);
        job.SignatureUrl.Should().Be("https://cdn/sig.png");
        job.Events.Should().Contain(e => e is JobCompletedDomainEvent);
    }

    [Fact]
    public void Complete_without_signature_is_rejected()
    {
        var job = NewDraftJob();
        job.Schedule(Now.AddDays(1), AssigneeId, Now);
        job.Start(Now.AddDays(1).AddHours(1));

        var r = job.Complete(null, Now.AddDays(1).AddHours(2));
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Job.SignatureRequired");
    }

    [Fact]
    public void Cannot_add_photo_after_Complete()
    {
        var job = NewDraftJob();
        job.Schedule(Now.AddDays(1), AssigneeId, Now);
        job.Start(Now.AddDays(1).AddHours(1));
        job.Complete("https://cdn/sig.png", Now.AddDays(1).AddHours(2));

        var r = job.AddPhoto("https://cdn/late.jpg", Now.AddDays(1).AddHours(3), null);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Job.CannotAddPhotoToTerminalJob");
    }

    [Fact]
    public void Cancel_from_Draft_transitions_to_Cancelled_and_raises_event()
    {
        var job = NewDraftJob();
        var r = job.Cancel("Customer withdrew", Now);
        r.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Cancelled);
        job.CancellationReason.Should().Be("Customer withdrew");
        job.Events.Should().Contain(e => e is JobCancelledDomainEvent);
    }

    [Fact]
    public void DrainEvents_returns_and_clears_events()
    {
        var job = NewDraftJob();
        job.Events.Should().HaveCount(1);
        var drained = job.DrainEvents();
        drained.Should().HaveCount(1);
        job.Events.Should().BeEmpty();
    }
}
