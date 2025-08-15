using AnnotatedDI.Attributes;
using AnnotatedDIExample.Services;

namespace MyApp.Services;

[Service]
[Profile(include: "dev", exclude:"prod")]
public class DevOnlyService : IDevOnlyService
{
    public void Execute() => Console.WriteLine("Dev-only service active!");
}