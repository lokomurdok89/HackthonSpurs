using HackthonSpurs.Application.Commands;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Tests;

internal static class AdmissionChecks
{
    /// <summary>Verify the ordering and conflict behaviors through in-memory admission and staging ports.</summary>
    public static async Task RunAsync()
    {
        List<string> events = [];
        using var original = new Original(events);
        var key = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var journal = new Admissions(events) { Existing = new(oldId, new(original.Sha256, false)) };
        var handler = new AcceptDocumentHandler(journal);

        Check.Require(await handler.HandleAsync(new(key, original, "unused-type", false), default) == new Receipt(Admission.Accepted, oldId),
            "Exact replay must return the committed ID before inspecting current MIME.");
        Check.Require(events.SequenceEqual(["find"]), "Replay must not validate, reserve, or publish.");
        Check.Require((await handler.HandleAsync(new(key, original, Media.Csv, true), default)).Outcome == Admission.Conflict,
            "A different lane conflicts even for identical bytes.");
        journal.Existing = new(oldId, new("unmatched", false));
        Check.Require((await handler.HandleAsync(new(key, original, Media.Csv, false), default)).Outcome == Admission.Conflict,
            "Different bytes must conflict with the same operation key.");

        journal.Existing = null;
        original.Inspection = new(null);
        events.Clear();
        Check.Require((await handler.HandleAsync(new(key, original, Media.Csv, false), default)).Outcome == Admission.Unsupported &&
            events.SequenceEqual(["find", "inspect"]), "Invalid format must never reserve or publish.");
        original.Inspection = new(Media.Png, TooLarge: true);
        events.Clear();
        Check.Require((await handler.HandleAsync(new(key, original, Media.Png, true), default)).Outcome == Admission.TooLarge &&
            events.SequenceEqual(["find", "inspect"]), "Format-specific limits must fail before durable admission.");

        original.Inspection = new(Media.Csv);
        journal.Winner = new(oldId, new(original.Sha256, false));
        events.Clear();
        Check.Require(await handler.HandleAsync(new(key, original, Media.Csv, false), default) == new Receipt(Admission.Accepted, oldId) &&
            events.SequenceEqual(["find", "inspect", "collide"]), "A matching concurrent winner must return its ID without publishing the loser.");
        journal.Winner = new(oldId, new(original.Sha256, true));
        Check.Require((await handler.HandleAsync(new(key, original, Media.Csv, false), default)).Outcome == Admission.Conflict,
            "The concurrent winner must run through the same mode/hash policy.");

        journal.Winner = null;
        events.Clear();
        Check.Require(await handler.HandleAsync(new(key, original, Media.Csv, false), default) == new Receipt(Admission.Accepted, original.Id) &&
            events.SequenceEqual(["find", "inspect", "reserve", "publish", "commit"]), "A new receipt must come after durable publication and commit.");
        Check.Require(journal.Pending == new PendingAdmission(key, original.Id, new(original.Sha256, false), Media.Csv, original.Length),
            "The admission adapter must receive the inspected type and stable receipt metadata.");
    }

    /// <summary>Track how the handler drives a staged original without using a filesystem.</summary>
    /// <param name="events">Shared record of staging and admission actions.</param>
    private sealed class Original(List<string> events) : IStagedOriginal
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Sha256 => "synthetic-hash";
        public long Length => 12;
        public OriginalInspection Inspection { get; set; } = new(Media.Csv);

        /// <inheritdoc />
        public OriginalInspection Inspect(string declaredType, bool spur)
        {
            events.Add("inspect");
            return Inspection;
        }

        /// <inheritdoc />
        public void Publish() => events.Add("publish");

        /// <summary>Dispose this in-memory test staged original.</summary>
        public void Dispose() { }
    }

    /// <summary>Model the winner-or-commit behavior guaranteed by the durable admission adapter.</summary>
    /// <param name="events">Shared record of staging and admission actions.</param>
    private sealed class Admissions(List<string> events) : IDocumentAdmissions
    {
        public AcceptedSubmission? Existing { get; set; }
        public AcceptedSubmission? Winner { get; set; }
        public PendingAdmission? Pending { get; private set; }

        /// <inheritdoc />
        public Task<AcceptedSubmission?> FindAsync(Guid key, CancellationToken ct)
        {
            events.Add("find");
            return Task.FromResult(Existing);
        }

        /// <inheritdoc />
        public Task<AcceptedSubmission?> CommitAsync(PendingAdmission pending, Action publishOriginal, CancellationToken ct)
        {
            Pending = pending;
            if (Winner is not null)
                events.Add("collide");
            else
            {
                events.Add("reserve");
                publishOriginal();
                events.Add("commit");
            }
            return Task.FromResult(Winner);
        }
    }
}
