using Contracts;
using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddDaprClient();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCloudEvents();
app.MapSubscribeHandler();

app.SubscribeToJobEvents();

app.Run();

public static class JobSchedulerEvents
{
	public static void SubscribeToJobEvents(this IEndpointRouteBuilder builder)
	{
		RouteGroupBuilder jobEventEndpoints = builder
			.MapGroup("JobEvents")
			.WithTags(new[] { "JobEvents" });

		// listen for new jobs that were created, and then start processing them
		jobEventEndpoints.MapPost("JobCreated",
			[Topic(Constants.JobsPubSubName, Constants.IncomingJobsTopic, "event.data.owner == \"microservice-x\"", 1)]
			async ([FromBody] JobCreated jobCreatedEvent) =>
			{
				Console.WriteLine(
					$"*** Received Event - JobCreated: {jobCreatedEvent.JobId} for {jobCreatedEvent.Owner}");
				return await Task.FromResult(Results.Ok());
			});

		// listen for jobs status changes
		jobEventEndpoints.MapPost("JobStatusChanged",
			[Topic(Constants.JobsPubSubName, Constants.JobStatusChangedTopic, "event.data.owner == \"microservice-x\"", 1)] 
			async (
				[FromBody] JobStatusChanged @event,
				[FromServices] DaprClient dapr) =>
			{
				Console.WriteLine($"*** Received Event - JobStatusChanged: {@event.JobId} from: {@event.OldStatus} to: {@event.NewStatus} for {@event.Owner}");
				return await Task.FromResult(Results.Ok());

			});

		// listen for jobs that were completed
		// this endpoint should live on each of the waiting microservices
		jobEventEndpoints.MapPost("JobCompleted", 
			[Topic(Constants.JobsPubSubName, Constants.CompletedJobsTopic, "event.data.owner == \"microservice-x\"", 1)] 
			async (
				[FromBody] JobCompleted @event,
				[FromServices] DaprClient dapr) =>
			{
				Console.WriteLine($"*** Received Event - JobCompleted: {@event.JobId}");
				return await Task.FromResult(Results.Ok());
			});
	}
}
