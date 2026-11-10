# Benchmarks

Run the benchmark script with PowerShell 7, with `bombardier`, `dotnet`, and
`go` on PATH (only the runtimes needed by the selected apps are required):

```powershell
.\benchmarks\scripts\RunBenchmarks.ps1
```

For example, to benchmark specific apps and connection counts:

```powershell
.\benchmarks\scripts\RunBenchmarks.ps1 -Apps FalcoBench,GoBench -Connections 64,256 -Runs 5
```

Results are saved under `benchmarks\results\<timestamp>\`. The script prints
the full paths to `summary.md` and `summary.json`; each measured run also has
a raw `<app>-c<connections>-run<run>.json` file in the same directory.
The Markdown summary groups results by connection count and separates
throughput and latency from resources and response counts for readability.

The script rebuilds selected apps under `benchmarks\build\`. It runs the load
generator locally, so the results also reflect contention for local resources.