using AnnotatedDI.Attributes;
using AnnotatedDIExample.Services;

namespace MyApp.Config;

[Configuration]
public class MyConfig
{
    [Bean]
    public DateTime Now() => DateTime.UtcNow;

    [Bean]
    [ConditionalOnProperty("app:sendmail", "true", false)] //name: "Features:EnableRepository", havingValue: "true"
    public INotifier MailSender() => new EmailService();

}