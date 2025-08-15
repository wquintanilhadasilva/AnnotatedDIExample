using AnnotatedDI.Attributes;
using AnnotatedDIExample.Attributes;
using AnnotatedDIExample.Repositories;

namespace MyApp.Repositories;

[Repository]
[ConditionalOnProperty(name: "Features:EnableRepository", havingValue: "true")]
public class MyRepository : IMyRepository
{
    public void Save() => Console.WriteLine("Repository active!");
}