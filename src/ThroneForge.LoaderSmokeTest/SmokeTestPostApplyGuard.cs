namespace ThroneForge.LoaderSmokeTest;

public static class SmokeTestPostApplyGuard
{
    public static SmokeTestPostApplyResult Execute(
        Func<LaunchObservationResult> launch,
        Func<string> readLog,
        Func<string, LoaderLogSummary> parseLog,
        Func<LoaderLogSummary, SmokeTestOutcome> classify,
        Func<bool> rollback,
        Action? writeRecoveryMarker = null,
        Action? prepareReport = null)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(readLog);
        ArgumentNullException.ThrowIfNull(parseLog);
        ArgumentNullException.ThrowIfNull(classify);
        ArgumentNullException.ThrowIfNull(rollback);

        LaunchObservationResult? observation = null;
        LoaderLogSummary? summary = null;
        try
        {
            observation = launch();
            if (observation.RequiresManualClosure && !observation.Exited)
            {
                var marker = TryPersistRecoveryMarker(writeRecoveryMarker);

                return new SmokeTestPostApplyResult(
                    SmokeTestOutcome.Inconclusive,
                    SmokeTestRollbackState.ManualClosureRequired,
                    observation,
                    null,
                    "manual-closure-required",
                    RecoveryMarkerPersisted: marker.Persisted,
                    RecoveryMarkerFailureCategory: marker.FailureCategory);
            }

            var log = readLog();
            summary = parseLog(log);
            var outcome = classify(summary);
            prepareReport?.Invoke();
            return CompleteWithRollback(outcome, observation, summary, rollback);
        }
        catch (Exception operationException)
        {
            if (observation?.RequiresManualClosure == true && !observation.Exited)
            {
                var marker = TryPersistRecoveryMarker(writeRecoveryMarker);

                return new SmokeTestPostApplyResult(
                    SmokeTestOutcome.Inconclusive,
                    SmokeTestRollbackState.ManualClosureRequired,
                    observation,
                    summary,
                    "manual-closure-required",
                    RecoveryMarkerPersisted: marker.Persisted,
                    RecoveryMarkerFailureCategory: marker.FailureCategory);
            }

            try
            {
                return rollback()
                    ? new SmokeTestPostApplyResult(
                        SmokeTestOutcome.Failed,
                        SmokeTestRollbackState.RollbackSucceeded,
                        observation,
                        summary,
                        "post-apply-operation-failed",
                        operationException)
                    : new SmokeTestPostApplyResult(
                        SmokeTestOutcome.Failed,
                        SmokeTestRollbackState.RollbackFailed,
                        observation,
                        summary,
                        "post-apply-operation-and-rollback-failed",
                        operationException);
            }
            catch (Exception rollbackException)
            {
                return new SmokeTestPostApplyResult(
                    SmokeTestOutcome.Failed,
                    SmokeTestRollbackState.RollbackFailed,
                    observation,
                    summary,
                    "post-apply-operation-and-rollback-failed",
                    operationException,
                    rollbackException);
            }
        }
    }

    private static (bool Persisted, string? FailureCategory) TryPersistRecoveryMarker(Action? writeRecoveryMarker)
    {
        if (writeRecoveryMarker is null)
        {
            return (false, "marker-unavailable");
        }

        try
        {
            writeRecoveryMarker();
            return (true, null);
        }
        catch (Exception)
        {
            return (false, "marker-write-failed");
        }
    }

    private static SmokeTestPostApplyResult CompleteWithRollback(
        SmokeTestOutcome outcome,
        LaunchObservationResult observation,
        LoaderLogSummary summary,
        Func<bool> rollback)
    {
        try
        {
            if (!rollback())
            {
                return new SmokeTestPostApplyResult(
                    SmokeTestOutcome.Failed,
                    SmokeTestRollbackState.RollbackFailed,
                    observation,
                    summary,
                    "rollback-failed");
            }

            return new SmokeTestPostApplyResult(
                outcome,
                SmokeTestRollbackState.RollbackSucceeded,
                observation,
                summary,
                "none");
        }
        catch (Exception rollbackException)
        {
            return new SmokeTestPostApplyResult(
                SmokeTestOutcome.Failed,
                SmokeTestRollbackState.RollbackFailed,
                observation,
                summary,
                "rollback-failed",
                null,
                rollbackException);
        }
    }
}
