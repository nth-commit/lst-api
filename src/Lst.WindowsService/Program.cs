using Lst.WindowsService;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddWindowsService(options =>
        {
            options.ServiceName = "Lst.Svc";
        });
    })
    .Build();

host.Run();
