var bldr = WebApplication.CreateSlimBuilder(args);

bldr.Logging.ClearProviders();
var wapp = bldr.Build();

wapp.MapGet("/", () => Results.RazorSlice<RazorSlicesBench.Views.Hello, DateTime>(DateTime.Now));

wapp.Run();
