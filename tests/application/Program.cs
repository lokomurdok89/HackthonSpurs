using HackthonSpurs.Application.Tests;

await AdmissionChecks.RunAsync();
await SearchChecks.RunAsync();
await ReadingChecks.RunAsync();
await IndexingChecks.RunAsync();
await WorkspaceChecks.RunAsync();
Console.WriteLine("PASS: offline application commands, semantic search and leased job attempts.");
