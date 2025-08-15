# 🔧 Annotated Dependency Injection for .NET

This project provides a lightweight annotation-based system for automatic service registration and configuration in ASP.NET Core (inspired by Spring Boot).

---

## ✨ Features

- `[Service]`: Registers a class as a singleton, scoped, or transient service.
- `[Configuration]`: Registers a class as a configuration class.
- `[Bean]`: Registers the method return as a service (bean), with dependencies auto-injected.
- `[Profile]`: Loads the bean only for matching profiles.
- `[ConditionalOnProperty]`: Loads the bean only if a config key matches a value.
- `[ComponentScan]`: Restrict scanning to specific namespaces or exclude others.
- Auto constructor injection (including `IEnumerable<T>`).
- Supports profile-based filtering using configuration (e.g. `appsettings.json`).
- Supports override or manual registration without conflict.

---

## 🛠️ Installation

1. Copy the source files to your solution.
2. Add reference to the assembly where needed.
3. Call `RegisterAnnotatedServices()` in `Program.cs`.

---

## 🚀 Quickstart

```csharp
builder.Services.RegisterAnnotatedServices(
    configuration: builder.Configuration,
    profilePropertyKey: "app.profiles"
);
```

To limit or exclude scan paths:

```csharp
builder.Services.RegisterAnnotatedServices(
    configuration: builder.Configuration,
    profilePropertyKey: "app.profiles",
    componentScan: new ComponentScanOptions {
        Include = new[] { typeof(SomeNamespace.MyMarkerClass) },
        Exclude = new[] { typeof(OtherNamespace.ShouldBeIgnored) }
    }
);
```

---

## 🔍 Attribute Reference

### `[Service]`

Registers a service with the desired lifetime.

```csharp
[Service(ServiceLifetime.Scoped)]
public class EmailService : IEmailService {}
```

If no lifetime is specified, `Transient` is used by default.

---

### `[Configuration]` + `[Bean]`

Creates bean-producing configuration classes.

```csharp
[Configuration]
public class AppConfig {

    [Bean]
    public IStrategy ProvideStrategy(IServiceA a, IServiceB b) {
        return new StrategyImpl(a, b);
    }
}
```

---

### `[ConditionalOnProperty]`

Conditional bean registration based on `IConfiguration`.

```csharp
[Service]
[ConditionalOnProperty("app.featureX.enabled", havingValue: "true", matchIfMissing: false)]
public class FeatureXService : IFeature {}
```

---

### `[Profile]`

Only registers if current profile matches.

```csharp
[Service]
[Profile(include: new[] { "prod", "stage" }, exclude: new[] { "dev" })]
public class RealEmailSender : IEmailSender {}
```

Set profile in `appsettings.json`:

```json
{
  "app": {
    "profiles": "prod"
  }
}
```

Then use it like:

```csharp
builder.Services.RegisterAnnotatedServices(
    builder.Configuration,
    profilePropertyKey: "app.profiles"
);
```

---

### `[ComponentScan]` (Attribute Usage)

Instead of passing `ComponentScanOptions` manually, you can annotate your startup class:

```csharp
[ComponentScan(
    Include = new[] { typeof(MyApp.Services.SomeService), typeof(MyApp.Repositories.SomeRepo) },
    Exclude = new[] { typeof(MyApp.Legacy.OldCode) }
)]
public class AppEntryPoint {}
```

Then in `Program.cs`:

```csharp
builder.Services.RegisterAnnotatedServices(
    builder.Configuration,
    profilePropertyKey: "app.profiles",
    componentScan: ComponentScanOptions.FromAttribute(typeof(AppEntryPoint))
);
```

---

## 🧠 Constructor Injection

Works with single instances or collections:

```csharp
[Service]
public class MyController {
    public MyController(IServiceA a, IEnumerable<IPlugin> plugins) { ... }
}
```

---

## 🧪 Example `appsettings.json`

```json
{
  "app": {
    "profiles": "dev",
    "featureX": {
      "enabled": "true"
    }
  }
}
```

---

## 📂 Project Structure

```
/Annotations
  └── ServiceAttribute.cs
  └── ConfigurationAttribute.cs
  └── BeanAttribute.cs
  └── ConditionalOnPropertyAttribute.cs
  └── ProfileAttribute.cs
  └── ComponentScanAttribute.cs
/Core
  └── ReflectionUtils.cs
  └── Scanner.cs
/Extensions
  └── ServiceCollectionExtensions.cs
  └── ComponentScanOptions.cs
```

---

## 🛡️ License

MIT License

---

## 🤝 Contributing

Pull requests and issues are welcome! Help grow this simple framework and share with the .NET community.