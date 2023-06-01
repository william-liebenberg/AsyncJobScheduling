using Contracts;
using Dapr;
using Dapr.Client;

namespace JobScheduler;

public record JobCheckState(int Attempts, JobStatus Status, int WaitTime);

public class JobSchedulingService
{
	private const int MaxAttemptsToRetrieveCompletedJob = 3;
	private const int InitialJobCheckStateWaitTimeMilliseconds = 10000;
	private const int AdditionalJobCheckStateWaitTimeMilliseconds = 5000;

	private readonly DaprClient _dapr;
	private readonly Random _random = Random.Shared;

	public JobSchedulingService(DaprClient dapr)
	{
		_dapr = dapr;
	}

	public async Task<Job?> GetJob(Guid jobId, CancellationToken cancellationToken)
	{
		try
		{
			var job = await _dapr.GetStateAsync<Job>(
				Constants.JobsStore,
				jobId.ToString(),
				cancellationToken: cancellationToken);

			return job;
		}
		catch (DaprException x)
		{
			Console.WriteLine(x.Message);
			return default;
		}
	}

	private async Task<Guid> CreateJob(string jobOwner, Dictionary<string, string> jobParameters, CancellationToken cancellationToken)
	{
		var job = new Job(
			JobId: Guid.NewGuid(),
			Owner: jobOwner,
			Status: JobStatus.New,
			Parameters: jobParameters);

		// save the initial job state
		await _dapr.SaveStateAsync(
			Constants.JobsStore,
			job.JobId.ToString(),
			job,
			cancellationToken: cancellationToken);

		// put it on the queue and listen right away
		await _dapr.PublishEventAsync(
			Constants.JobsPubSubName,
			Constants.IncomingJobsTopic,
			new JobCreated(job.JobId, job.Owner),
			cancellationToken: cancellationToken);

		return job.JobId;
	}

	public async Task<(Guid jobId, Job? job)> CreateJobAndReturnImmediately(JobRequest request, CancellationToken cancellationToken)
	{
		Guid newJobId = await CreateJob(request.Owner, request.Parameters, cancellationToken);

		// Start processing the job right away
		var processingTask = Process(newJobId, cancellationToken);

		return (newJobId, null);
	}

	public async Task<(Guid jobId, Job? job)> CreateJobAndReturnResultIfAvailable(JobRequestWithTimeout request, CancellationToken cancellationToken)
	{
		// TODO: Validate the request - especially the timeout
		// TODO: The job id / request token should come from the caller
		// TODO: can we somehow return a "hint" for how long the caller should wait?

		Guid newJobId = await CreateJob(request.Owner, request.Parameters, cancellationToken);
		
		// Start processing the job right away
		var processingTask = Process(newJobId, cancellationToken);
		if (await Task.WhenAny(processingTask, Task.Delay(request.InitialTimeout, cancellationToken)) != processingTask)
		{
			// timeout/cancellation logic
			return (newJobId, null);
		}

		// Task completed within timeout.
		// Consider that the task may have faulted or been canceled.
		// We re-await the task so that any exceptions/cancellation is rethrown.
		await processingTask;

		Job? processedJob = await GetJob(newJobId, cancellationToken);
		return processedJob switch
		{
			not null => (processedJob.JobId, processedJob),
			_ => (newJobId, null)
		};
	}

	public async Task Process(Guid jobId, CancellationToken cancellationToken)
	{
		Job? job = await GetJob(jobId, cancellationToken);

		if (job is null)
		{
			Console.WriteLine($"Could not find Job {jobId}... go away!");
			return;
		}

		if (job.Status != JobStatus.New)
		{
			Console.WriteLine("Can only process NEW jobs... go away!");
			return;
		}

		// TODO: Should the job also be added to a "jobs in progress" queue?

		await UpdateJobStatus(job, JobStatus.Processing, cancellationToken);

		// --------------------------------- FAKE PROCESSING ----------------------------------
		var delayMs = _random.Next(100, 10000);
		Console.WriteLine($"--- Processing job {job.JobId} - delaying for {delayMs}ms...");
		await Task.Delay(delayMs, cancellationToken);
		// TODO: Randomly throw some exceptions to trigger the error/failure handling logic
		Console.WriteLine($"--- Processing job {job.JobId} - updating status to Completed");
		// --------------------------------- FAKE PROCESSING ----------------------------------

		await UpdateJobStatus(job, JobStatus.Completed, cancellationToken);

		var completedJob = await GetJob(job.JobId, cancellationToken);
		if (completedJob is not null)
		{
			if (completedJob.Status == JobStatus.Completed)
			{
				await _dapr.PublishEventAsync(
					Constants.JobsPubSubName,
					Constants.CompletedJobsTopic,
					new JobCompleted(completedJob.JobId, completedJob.Owner, TimeSpan.FromHours(1), new Uri("http://blahhh.com")),
					cancellationToken: cancellationToken);
			}
		}

		// TODO: Failure handling logic
		
	}

	private async Task UpdateJobStatus(Job job, JobStatus newStatus, CancellationToken cancellationToken)
	{
		var updatedJob = job with { Status = newStatus };

		await _dapr.SaveStateAsync(
			Constants.JobsStore,
			updatedJob.JobId.ToString(),
			updatedJob,
			cancellationToken: cancellationToken);

		await _dapr.PublishEventAsync(
			Constants.JobsPubSubName,
			Constants.JobStatusChangedTopic,
			new JobStatusChanged(
				JobId: job.JobId,
				Owner: job.Owner,
				OldStatus: job.Status,
				NewStatus: updatedJob.Status),
			cancellationToken: cancellationToken);
	}
}