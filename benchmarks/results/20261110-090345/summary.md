# Benchmark results (20261110-090345)

- Date: 2026-11-10 09:17:04Z
- OS: Microsoft Windows 10.0.19045
- CPU cores: 4
- .NET SDK: 10.0.114
- Go: go version go1.26.0 windows/amd64
- Duration: 30s x 3 runs (median reported), warmup 10s, idle 30s

## 64 connections

Throughput and latency

| App              | Req/s median | min   | max   | p50 ms | p99 ms |
| ---------------- | -----------: | ----: | ----: | -----: | -----: |
| RazorSlicesBench |        86778 | 86240 | 87988 |      1 |      1 |
| FalcoBench       |        80074 | 77606 | 81166 |      1 |   1.99 |
| GoBench          |        49263 | 49031 | 50414 |   1.15 |   3.63 |

Resources and responses

| App              | Peak MB | Idle MB | Startup ms | Publish MB | Non-200 |
| ---------------- | ------: | ------: | ---------: | ---------: | ------: |
| RazorSlicesBench |    49.8 |    40.8 |       1305 |         13 |       0 |
| FalcoBench       |    49.7 |    42.8 |       1287 |       13.1 |       0 |
| GoBench          |    17.2 |    15.5 |       1366 |        8.2 |       0 |

## 256 connections

Throughput and latency

| App              | Req/s median | min   | max   | p50 ms | p99 ms |
| ---------------- | -----------: | ----: | ----: | -----: | -----: |
| RazorSlicesBench |        68749 | 67939 | 70096 |   3.99 |   4.99 |
| FalcoBench       |        62976 | 62691 | 63327 |   3.99 |   6.98 |
| GoBench          |        48109 | 47973 | 48866 |   4.59 |  13.97 |

Resources and responses

| App              | Peak MB | Idle MB | Startup ms | Publish MB | Non-200 |
| ---------------- | ------: | ------: | ---------: | ---------: | ------: |
| RazorSlicesBench |    79.9 |    61.7 |       1239 |         13 |       0 |
| FalcoBench       |    71.1 |    49.4 |       1228 |       13.1 |       0 |
| GoBench          |    24.6 |    23.9 |        519 |        8.2 |       0 |
