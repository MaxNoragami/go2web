using go2web.Commands;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<Commands>();
app.Run(args);
