using AnnotatedDIExample.Attributes;

[ComponentScan(typeof(MyApp.Services.MyService), typeof(MyApp.Repositories.MyRepository))]
public class Root { }