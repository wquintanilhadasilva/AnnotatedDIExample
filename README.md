# Annotated Dependency Injection Framework

A lightweight attribute-based Dependency Injection (DI) framework for .NET, inspired by Spring Boot annotations.

## Features

- **@Service**: Marks a class as a service component for DI registration.
- **@Repository**: Marks a class as a repository component for DI registration.
- **@Configuration**: Marks a class that contains bean creation methods.
- **@Bean**: Marks a method inside a configuration class for creating custom beans.
- **@Profile**: Conditional registration based on active environment profiles.
- **@ConditionalOnProperty**: Conditional registration based on configuration properties.
- Dependency graph resolution and ordering to ensure proper instantiation.
- Supports injection of multiple implementations via `IEnumerable<T>`.

---

## Minimal Example

### Service
```csharp
[Service]
public class MyService : IMyService
{
    public void Execute() => Console.WriteLine("MyService active!");
}
```

### Repository
```csharp
[Repository]
public class MyRepository : IMyRepository
{
    public void Save() => Console.WriteLine("Repository active!");
}
```

### Program
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.ScanWithAnnotatedDI(builder.Configuration, "MyAppNamespace");
var app = builder.Build();
app.Run();
```

---

## Advanced Example

### Conditional Service with Profile and Repository
```csharp
[Service]
[Profile(include: "dev", exclude: "prod")]
public class MyService : IMyService
{
    private readonly IMyRepository repository;

    public MyService(IMyRepository repository)
    {
        this.repository = repository;
    }

    public void Execute() => Console.WriteLine("MyService active!");
}

[Repository]
[ConditionalOnProperty(name: "Features:EnableRepository", havingValue: "true")]
public class MyRepository : IMyRepository
{
    public void Save() => Console.WriteLine("Repository active!");
}
```

### Configuration with Beans
```csharp
[Configuration]
public class MyConfiguration
{
    [Bean]
    public ICustomBean CreateCustomBean()
    {
        return new CustomBean();
    }
}

public class CustomBean : ICustomBean
{
    public void DoSomething() => Console.WriteLine("Custom bean working!");
}
```

### Multiple Implementations Injection
```csharp
[Service]
public class FirstHandler : IHandler
{
    public void Handle() => Console.WriteLine("First handler");
}

[Service]
public class SecondHandler : IHandler
{
    public void Handle() => Console.WriteLine("Second handler");
}

[Service]
public class HandlerManager
{
    private readonly IEnumerable<IHandler> handlers;

    public HandlerManager(IEnumerable<IHandler> handlers)
    {
        this.handlers = handlers;
    }

    public void RunAll()
    {
        foreach (var handler in handlers)
            handler.Handle();
    }
}
```

---

## Rules

1. **Service / Repository Registration**  
   - Registers the first implemented interface in DI.
   - All implementations are instantiated.
   - If `IEnumerable<T>` is injected, all implementations are provided.

2. **Configuration and Beans**  
   - `@Configuration` classes are instantiated without DI injection.
   - `@Bean` methods are executed after service/repository creation.
   - Beans can depend on any registered service/repository.

3. **Dependency Graph**  
   - All dependencies are resolved and instantiated in topological order.
   - Cyclic dependencies cause an error with detailed information.

---

## Example Settings
```json
{
  "Features": {
    "EnableRepository": "true"
  },
  "Profiles": "dev"
}
```

---

## Bootstrapping
```csharp
builder.Services.ScanWithAnnotatedDI(builder.Configuration, "MyAppNamespace");
```
