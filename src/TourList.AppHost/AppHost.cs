var builder = DistributedApplication.CreateBuilder(args);

// Azure SQL in the cloud; a local SQL Server container during development.
var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer();

var tourlistDb = sql.AddDatabase("tourlist");

builder.AddProject<Projects.TourList_Server>("server")
    .WithReference(tourlistDb)
    .WaitFor(tourlistDb);

builder.Build().Run();
