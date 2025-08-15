using AnnotatedDI.Attributes;
using AnnotatedDIExample.Services;

namespace AnnotatedDIExample.Config;

[Configuration]
public class ConfigurationWithParameter
{

    private readonly IMyService service;

    public ConfigurationWithParameter(IMyService service)
    {
        this.service = service;
    }
}