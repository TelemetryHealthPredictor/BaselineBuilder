using BaselineBuilderNew.Build;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddAppServices();

IHost host = builder.Build();

host.Run();