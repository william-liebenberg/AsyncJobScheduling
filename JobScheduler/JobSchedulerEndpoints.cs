using Contracts;
using Microsoft.AspNetCore.Mvc;

namespace JobScheduler;

public record JobRequest(string Owner, Dictionary<string, string> Parameters);
public record JobRequestWithTimeout(string Owner, int InitialTimeout, Dictionary<string, string> Parameters);


public static class JobSchedulerEndpoints
{
	public static void MapJobSchedulingEndpoints(this IEndpointRouteBuilder builder)
	{
		RouteGroupBuilder jobEndpoints = builder
			.MapGroup("Jobs")
			.WithTags(new[] { "Jobs" });

		// HTTP Endpoint for starting new jobs
		jobEndpoints.MapPost("StartJobWithTimeout", async (
			[FromBody] JobRequestWithTimeout request,
			[FromServices] JobSchedulingService jobScheduler, 
				CancellationToken cancellationToken) =>
		{
			(Guid jobId, Job? job) result = await jobScheduler.CreateJobAndReturnResultIfAvailable(request, cancellationToken);
			Console.WriteLine($"Created Job {result.jobId} for {request.Owner}");

			if (result.job is not null)
			{
				// if job creation returned a result -> return 200 + payload
				Console.WriteLine($"Job {result.jobId} Completed early enough (returning OK/200)");
				return await Task.FromResult(Results.Ok(result.job));
			}

			// if job creation returned no result -> return 202 + jobId
			Console.WriteLine($"Job {result.jobId} has not completed early enough...(returning Accepted/202)");
			return await Task.FromResult(Results.Accepted($"/Jobs/get?jobId={result.jobId}", result.jobId));
		})
			.Produces(200, typeof(Job), "application/json")
			.Produces(202, typeof(Guid), "application/json");

		jobEndpoints.MapPost("StartJob", async (
				[FromBody] JobRequest request,
				[FromServices] JobSchedulingService jobScheduler,
				CancellationToken cancellationToken) =>
			{
				(Guid jobId, Job? job) result = await jobScheduler.CreateJobAndReturnImmediately(request, cancellationToken);
				Console.WriteLine($"Created Job {result.jobId} for {request.Owner}");

				if (result.job is not null)
				{
					// if job creation returned a result -> return 200 + payload
					Console.WriteLine($"Job {result.jobId} Completed early enough (returning OK/200)");
					return await Task.FromResult(Results.Ok(result.job));
				}

				// if job creation returned no result -> return 202 + jobId
				Console.WriteLine($"Job {result.jobId} has not completed early enough...(returning Accepted/202)");
				return await Task.FromResult(Results.Accepted($"/Jobs/get?jobId={result.jobId}", result.jobId));
			})
			.Produces(200, typeof(Job), "application/json")
			.Produces(202, typeof(Guid), "application/json");



		// HTTP GET for polling job status
		jobEndpoints.MapGet("get", async (
			[FromQuery] Guid jobId,
			[FromServices] JobSchedulingService jobScheduler,
			CancellationToken cancellationToken) =>
		{
			var job = await jobScheduler.GetJob(jobId, cancellationToken);
			return job switch
			{
				null => Results.NotFound(),
				_ => Results.Ok(job)
			};
		});
	}

	

	//public static void SubscribeToJobEvents(this IEndpointRouteBuilder builder)
	//{
	//	RouteGroupBuilder jobEventEndpoints = builder
	//		.MapGroup("JobEvents")
	//		.WithTags(new[] { "JobEvents" });

	//	// listen for new jobs that were created, and then start processing them
	//	jobEventEndpoints.MapPost("JobCreated", async (
	//		[FromBody] JobCreated jobCreatedEvent,
	//		[FromServices] JobSchedulingService jobScheduler,
	//		CancellationToken cancellationToken) =>
	//	{
	//		Console.WriteLine($"*** Received Event - JobCreated: {jobCreatedEvent.JobId} for {jobCreatedEvent.Owner}");
	//		await jobScheduler.Process(jobCreatedEvent.JobId, cancellationToken);
	//		return await Task.FromResult(Results.Ok());
	//	}).WithTopic(Constants.JobsPubSubName, Constants.IncomingJobsTopic);
	//}
}