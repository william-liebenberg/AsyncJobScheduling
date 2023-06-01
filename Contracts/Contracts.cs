namespace Contracts;

public static class Constants
{
	public const string JobsStore = "nw.store.jobs";

	public const string JobsPubSubName = "nw.pubsub.jobs";
	public const string IncomingJobsTopic = "nw.jobs.incoming";
	public const string CompletedJobsTopic = "nw.jobs.completed";
	public const string JobStatusChangedTopic = "nw.jobs.status";
}

public enum JobStatus
{
	New,
	Processing,
	Completed,
	Failed,
	Aborted
};


public record JobCreated(Guid JobId, string Owner);
public record Job(Guid JobId, string Owner, JobStatus Status, Dictionary<string, string> Parameters);
public record JobCompleted(Guid JobId, string Owner, TimeSpan Duration, Uri PayloadUri);
public record JobStatusChanged(Guid JobId, string Owner, JobStatus OldStatus, JobStatus NewStatus);

