using AnnotatedDI.Attributes;
using AnnotatedDIExample.Attributes;

namespace AnnotatedDIExample.Services
{

    public class EmailService : INotifier
    {
        public void Notify(string message) => Console.WriteLine($"Email: {message}");
    }
}
