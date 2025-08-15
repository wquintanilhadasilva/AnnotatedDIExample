using AnnotatedDI.Attributes;
using AnnotatedDIExample.Attributes;

namespace AnnotatedDIExample.Services
{
    [Service]
    [Qualifier("sms")]
    [ConditionalOnProperty("app:sendmail", "false", true)]
    public class SmsNotification : INotifier
    {
        public void Notify(string message) => Console.WriteLine($"SMS: {message}");
    }
}
