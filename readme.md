# Async Job Scheduling API

This sample project demonstrates how a User Application (e.g. portal webapp) or a Microservice can start long running jobs.

## Asynchronous Request-Reply (ARR) pattern

The [asynchronous request-reply pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply) is a great way for dealing with potentially long running requests and solves the major issue of HTTP requests timing out and losing an important transaction.

In a nutshell, the ARR pattern requires the use of a queue (in-memory or persisted) that allows the caller to instantly receive a response (typically a `202 Accepted`) with an ID or URI that allows them to periodically poll the service for a completed result (`200 OK`).

### Scenario 1: User on a web portal sending a long-request to the backend

Depending on the DX/UX requirements we can introduce the ability for users / services calling the API to wait for a period of time (that they can specify themselves) to allow the request to complete.

If the request can complete within the desired wait time, then a `200 OK` response is returned along with any relevant payloads.

If the desired wait time elapses then the API does not timeout or drop the request. The API continues processing in the background and returns a useful `202 Accepted` response to the caller. The response typically includes an ID as the resulting payload, and a URI in the `Location` header for obtaining the result at a later time.

In this scenario, the calling applications can use the `Location` header to periodically poll the Job Scheduling API to check the status of the job before continuing on with another process or to give the users an update on the UI to show that their request is still being processed.

### Scenario 2 - Microservices requesting data that requires a long running process

The ARR pattern works very well in the scenario where microservices are relying on the response from a long-running request. However, the process can be a bit simpler than polling scenario.

Before sending the requests, Microservices can `Subscribe` to a `Queue` or `Topic` to receive messages about `Completed` jobs. Queues and Topics can also be used to indicate progress/state changes as the long-running job is processing.

Subscribing to the Queues or Topics avoids having to continuously poll the API to check for completion.

### Commonalities

Regardless of the scenario, for long-running jobs it is useful to track (and persist) the state of the work being done. Typically some sort of simple `State Management` (key-value pair / NoSQL storage) should be used to save and retrieve the state of the jobs.

The job state typically includes the original Job ID, input parameters, current processing state (New, InProgress, Completed, Failed etc.). The Job Scheduling API can publish event payloads to various Queues or Topics to indicate when jobs are Created, Updated, Completed or Failed. Other services can (if they have access) subscribe to the Topics and react to any of the state changes as required.

## Technology details

The sample app is built with [.NET 7](https://dotnet.microsoft.com/en-us/download/dotnet) and is using [Dapr](https://dapr.io) to provide some abstractions around State Management and Event/Message Publishing.

## Running the sample project

The sample projects can be started with the supplied `start.cmd` file. Just run it :) but there are some prerequisites for it to run successfully.

Once the services are up and running:

1. Open the JobOrchestrator API: [https://localhost:7104/swagger](https://localhost:7104/swagger)
2. Start a new Job that will wait for a given period of time with `/Jobs/StartJobWithTimeout` endpoint
3. Notice the logs will display the progress of the job processing with Status Updates
4. The Request will initially wait for a period of time (as specified in the request payload) and return either a 202 or 204 depending on how long the job takes to complete.
5. Notice that the second microservice (`MicroserviceX`) does not display any logs
6. Create a new job and specify `microservice-x` as the owner
7. Now notice that the second microservice is receiving events and printing logs

### Prerequisites

1. [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet)
2. [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or just the free Docker daemon)
3. [Dapr](https://dapr.io)
4. [CosmosDB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator) (for local dev)
   - you can swap `CosmosDB` for `Redis` or any other Dapr state store as long as you define it in the `components/statestore.yaml` file

### Steps

1. Create a `secrets.json` file in the root of the project to store the `masterKey` for connecting to your CosmosDB instance

```json
{
    "cosmosKey": "C2y6yDjf5........"
}
```

2. Install Dapr CLI

```ps1
winget install Dapr.CLI
```

3. Initialize Dapr

```ps1
dapr init
```

This will download the Dapr placement docker image, as well as a Redis (for running state, pubsub and other building blocks locally) and the Zipkin image for some Dapr observability.

4. Navigate to your project folder

```ps1
cd /source/repos/joborchestrator
```

5. Start the Dapr Sidecars and launch the .NET WebApi applications

```ps1
start.cmd
```

The `start.cmd` command file will start new terminal windows (1 per application). Each terminal will show the Dapr and ASP.NET logs and console output.

6. To Debug your applications with Visual Studio, go to `Debug > Attach to Process` (`Ctrl+Alt+P`) and select the `JobOrchestrator.exe` and `MicroserviceX.exe` processes and click `Attach`

Happy microservicing!
