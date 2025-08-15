using AnnotatedDI.Attributes;
using AnnotatedDIExample.Repositories;
using AnnotatedDIExample.Services;

namespace MyApp.Services;

[Service]
[Profile(include: "dev", exclude: "prod")]
public class MyService : IMyService
{
    private readonly IMyRepository repository;
    private readonly INotifier notifier;

    public MyService(IMyRepository repository, INotifier notifier)
    {
        this.repository = repository;
        this.notifier = notifier;
    }

    public void Execute()
    {
        Console.WriteLine("MyService active!");
        notifier.Notify("Notify from MyService");
        repository.Save();
    }
}