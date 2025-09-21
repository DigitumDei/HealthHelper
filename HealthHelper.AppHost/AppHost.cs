var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.HealthHelper>("healthhelper");

builder.Build().Run();
