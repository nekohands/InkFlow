using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using InkFlow.Api;
using InkFlow.ConsistencyFixtures;

var output = args.Length > 0 ? args[0] : "artifacts/consistency/rules.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var measurements = new List<object>();
foreach (var size in new[] { 0, 1_000, 10_000, 100_000 })
foreach (var corrupt in new[] { false, true })
{
    var snapshot = ConsistencyFixture.Create(size, corrupt);
    var service = new ConsistencyCheckService(new ConsistencyFixture.Reader(snapshot), new ConsistencyFixture.Clock());
    var completeHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ConsistencyCheckValidator.Validate(snapshot))));
    var report = await service.CheckAsync();
    var reportHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(report)));
    for (var round = 1; round <= 3; round++)
    {
        for (var warmup = 0; warmup < 3; warmup++) await service.CheckAsync();
        var samples = new List<object>();
        var elapsed = new List<double>();
        for (var sample = 0; sample < 30; sample++)
        {
            var before = GC.GetTotalAllocatedBytes(true);
            var start = Stopwatch.GetTimestamp();
            var actual = await service.CheckAsync();
            var milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes(true) - before;
            if (Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(actual))) != reportHash)
                throw new InvalidOperationException("Report changed during measurement.");
            elapsed.Add(milliseconds);
            samples.Add(new { milliseconds, allocated, processLifetimePeakBytes = Process.GetCurrentProcess().PeakWorkingSet64 });
        }
        elapsed.Sort();
        measurements.Add(new { size, corrupt, round, completeHash, reportHash, report.TotalIssueCount, p50 = elapsed[14], p95 = elapsed[28], samples });
        Console.WriteLine($"size={size} corrupt={corrupt} round={round} p50={elapsed[14]:F3}ms p95={elapsed[28]:F3}ms issues={report.TotalIssueCount} complete={completeHash}");
    }
}
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
{
    mode = "rules-only", framework = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription, cpuCount = Environment.ProcessorCount,
    warmups = 3, samplesPerRound = 30, rounds = 3, concurrency = 1, measurements,
}, new JsonSerializerOptions { WriteIndented = true }));
