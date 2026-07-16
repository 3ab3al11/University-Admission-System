using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class AdmissionsScheduleRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 13, 12, 0, 0);

    [Fact]
    public void ManualCloseOverridesAnOtherwiseOpenSchedule()
    {
        var result = Evaluate(
            manualOpen: false,
            startAt: Now.AddDays(-1),
            endAt: Now.AddDays(1));

        AssertStatus(result, false, AdmissionsGateState.ManualClosed);
    }

    [Fact]
    public void NoScheduleUsesManualOpenState()
    {
        var result = Evaluate(manualOpen: true);

        AssertStatus(result, true, AdmissionsGateState.ManualOpen);
    }

    [Fact]
    public void StartOnlyScheduleIsClosedBeforeStart()
    {
        var result = Evaluate(manualOpen: true, startAt: Now.AddMinutes(1));

        AssertStatus(result, false, AdmissionsGateState.NotStarted);
    }

    [Fact]
    public void StartOnlyScheduleOpensAtStartBoundary()
    {
        var result = Evaluate(manualOpen: true, startAt: Now);

        AssertStatus(result, true, AdmissionsGateState.Open);
    }

    [Fact]
    public void StartOnlyScheduleRemainsOpenAfterStart()
    {
        var result = Evaluate(manualOpen: true, startAt: Now.AddDays(-1));

        AssertStatus(result, true, AdmissionsGateState.Open);
    }

    [Fact]
    public void EndOnlyScheduleIsOpenBeforeEnd()
    {
        var result = Evaluate(manualOpen: true, endAt: Now.AddMinutes(1));

        AssertStatus(result, true, AdmissionsGateState.Open);
    }

    [Fact]
    public void EndOnlyScheduleStaysOpenAtEndBoundary()
    {
        var result = Evaluate(manualOpen: true, endAt: Now);

        AssertStatus(result, true, AdmissionsGateState.Open);
    }

    [Fact]
    public void EndOnlyScheduleExpiresAfterEnd()
    {
        var result = Evaluate(manualOpen: true, endAt: Now.AddMinutes(-1));

        AssertStatus(result, false, AdmissionsGateState.Expired);
    }

    [Fact]
    public void FullScheduleIsClosedBeforeWindow()
    {
        var result = Evaluate(
            manualOpen: true,
            startAt: Now.AddMinutes(1),
            endAt: Now.AddDays(1));

        AssertStatus(result, false, AdmissionsGateState.NotStarted);
    }

    [Fact]
    public void FullScheduleIsOpenInsideWindow()
    {
        var startAt = Now.AddDays(-1);
        var endAt = Now.AddDays(1);

        var result = Evaluate(manualOpen: true, startAt: startAt, endAt: endAt);

        AssertStatus(result, true, AdmissionsGateState.Open);
        Assert.Equal(startAt, result.StartAt);
        Assert.Equal(endAt, result.EndAt);
    }

    [Fact]
    public void FullScheduleExpiresAfterWindow()
    {
        var result = Evaluate(
            manualOpen: true,
            startAt: Now.AddDays(-2),
            endAt: Now.AddMinutes(-1));

        AssertStatus(result, false, AdmissionsGateState.Expired);
    }

    private static AdmissionsGateStatus Evaluate(
        bool manualOpen,
        DateTime? startAt = null,
        DateTime? endAt = null) =>
        AdmissionsScheduleRules.Evaluate(manualOpen, Now, startAt, endAt);

    private static void AssertStatus(
        AdmissionsGateStatus result,
        bool isOpen,
        AdmissionsGateState state)
    {
        Assert.Equal(isOpen, result.IsOpen);
        Assert.Equal(state, result.State);
    }
}
