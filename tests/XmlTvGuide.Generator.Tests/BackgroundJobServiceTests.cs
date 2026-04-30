using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using xmlTVGuide.Services.BackgroundJobs;
using xmlTVGuide.Services.BuildJobLogger;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class BackgroundJobServiceTests
{
    [Fact]
    public async Task starts_and_completes_successful_job()
    {
        var logger = new Mock<IBuildJobLogger>();
        var service = new BackgroundJobService(logger.Object, NullLogger<BackgroundJobService>.Instance);
        var completion = new TaskCompletionSource();

        var (canStart, message) = await service.TryStartJobAsync(() =>
        {
            completion.SetResult();
            return Task.CompletedTask;
        }, "EPG Rebuild");

        canStart.Should().BeTrue();
        message.Should().Contain("started successfully");
        await completion.Task;
        await WaitUntilAsync(() => !service.GetCurrentStatus().IsRunning);

        service.GetCurrentStatus().IsRunning.Should().BeFalse();
        service.GetCurrentStatus().CurrentMessage.Should().Contain("completed successfully");
        logger.Verify(l => l.LogBuildJob(
            It.IsAny<DateTime>(),
            It.IsAny<DateTime?>(),
            It.IsAny<TimeSpan?>(),
            true,
            It.Is<string>(s => s.Contains("completed successfully")),
            null), Times.Once);
    }

    [Fact]
    public async Task rejects_second_job_while_first_is_running()
    {
        var logger = new Mock<IBuildJobLogger>();
        var service = new BackgroundJobService(logger.Object, NullLogger<BackgroundJobService>.Instance);
        var gate = new TaskCompletionSource();

        await service.TryStartJobAsync(async () => await gate.Task, "Long Job");
        var (canStart, message) = await service.TryStartJobAsync(() => Task.CompletedTask, "Second Job");

        canStart.Should().BeFalse();
        message.Should().Contain("already running");
        gate.SetResult();
        await WaitUntilAsync(() => !service.GetCurrentStatus().IsRunning);
    }

    [Fact]
    public async Task logs_failure_when_job_throws()
    {
        var logger = new Mock<IBuildJobLogger>();
        var service = new BackgroundJobService(logger.Object, NullLogger<BackgroundJobService>.Instance);

        await service.TryStartJobAsync(() => throw new InvalidOperationException("boom"), "Broken Job");
        await WaitUntilAsync(() => !service.GetCurrentStatus().IsRunning);

        service.GetCurrentStatus().CurrentMessage.Should().Contain("boom");
        logger.Verify(l => l.LogBuildJob(
            It.IsAny<DateTime>(),
            It.IsAny<DateTime?>(),
            It.IsAny<TimeSpan?>(),
            false,
            It.Is<string>(s => s.Contains("failed")),
            "boom"), Times.Once);
    }

    [Fact]
    public async Task logs_cancellation_when_job_throws_operation_canceled()
    {
        var logger = new Mock<IBuildJobLogger>();
        var service = new BackgroundJobService(logger.Object, NullLogger<BackgroundJobService>.Instance);

        await service.TryStartJobAsync(() => throw new OperationCanceledException(), "Cancelable Job");
        await WaitUntilAsync(() => !service.GetCurrentStatus().IsRunning);

        service.GetCurrentStatus().CurrentMessage.Should().Contain("cancelled");
        logger.Verify(l => l.LogBuildJob(
            It.IsAny<DateTime>(),
            It.IsAny<DateTime?>(),
            It.IsAny<TimeSpan?>(),
            false,
            It.Is<string>(s => s.Contains("cancelled")),
            "Job was cancelled by user"), Times.Once);
    }

    [Fact]
    public void get_history_maps_build_logger_entries()
    {
        var buildLogger = new Mock<IBuildJobLogger>();
        buildLogger.Setup(logger => logger.GetLastJobs(10)).Returns(new List<BuildJobEntry>
        {
            new()
            {
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Duration = TimeSpan.FromSeconds(1),
                Success = true,
                Message = "ok"
            }
        });

        var service = new BackgroundJobService(buildLogger.Object, NullLogger<BackgroundJobService>.Instance);
        var history = service.GetHistory(10);

        history.Should().ContainSingle(entry => entry.Message == "ok" && entry.Success);
    }

    [Fact]
    public void cancel_current_does_not_throw_when_no_job_is_running()
    {
        var service = new BackgroundJobService(new Mock<IBuildJobLogger>().Object, NullLogger<BackgroundJobService>.Instance);

        Action act = () => service.CancelCurrent();
        act.Should().NotThrow();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
                return;

            await Task.Delay(20);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
