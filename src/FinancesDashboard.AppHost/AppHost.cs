var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var apiService = builder.AddProject<Projects.FinancesDashboard_ApiService>("apiservice")
    .WithHttpHealthCheck("/api/health");

builder.AddProject<Projects.FinancesDashboard_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
