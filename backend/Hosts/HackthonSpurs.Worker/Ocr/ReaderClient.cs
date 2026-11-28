using HackthonSpurs.Worker.Ocr.Contracts;
using HackthonSpurs.Worker.Ocr.Rendering;
using HackthonSpurs.Application.Processing;
using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Storage;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace HackthonSpurs.Worker.Ocr;

/// <summary>Send verified original bytes to the private reader for this lane.</summary>
/// <param name="http">Configured transport.</param>
/// <param name="endpoint">Private reader endpoint.</param>
/// <param name="root">Private originals directory.</param>
/// <param name="bands">Local image and wire budget planner.</param>
internal sealed class ReaderClient(HttpClient http, Uri endpoint, string root, BandPlanner bands) : IDocumentReader
{
    /// <summary>Verify the original, call the bounded reader and validate its response.</summary>
    /// <param name="job">Claim authorizing this attempt.</param>
    /// <param name="stopping">Host shutdown token.</param>
    public async Task<ReadOutcome> ReadAsync(ReadingClaim job, CancellationToken stopping)
    {
        bool spur = endpoint.Host == ServiceDefaults.SpurHost;
        byte[] bytes;
        try
        {
            using var input = File.OpenRead(Path.Combine(root, job.Id.ToString("N") + Wire.OriginalSuffix));
            if (job.ByteCount <= 0 || job.ByteCount > (spur ? OcrFormats.MaxDocumentBytes : Policy.MaxFixtureBytes) || input.Length != job.ByteCount)
                return new(Error: Errors.OriginalMismatch.Code);
            bytes = new byte[(int)job.ByteCount];
            await input.ReadExactlyAsync(bytes, stopping);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != (spur ? job.Hash : FixtureLane.Hash))
                return new(Error: Errors.OriginalMismatch.Code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(Error: Errors.OriginalUnavailable.Code, Retryable: true); }

        if (!spur || (job.MediaType is Media.Png or Media.Jpeg && bands.Fits(bytes, job.MediaType)))
            return await SendAsync(job.Id, bytes, job.MediaType, spur ? 1 : 0, stopping);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        deadline.CancelAfter(TimeSpan.FromMinutes(Policy.DocumentMinutes));
        try
        {
            var dir = Directory.CreateTempSubdirectory(RenderSettings.TempPrefix);
            try
            {
                bool direct = job.MediaType is Media.Png or Media.Jpeg;
                var media = direct ? job.MediaType : Media.Png;
                var pages = direct
                    ? new[] { await LosslessPages.StageAsync(bytes, media, dir.FullName, deadline.Token) }
                    : await DocumentRenderer.RenderAsync(bytes, media: job.MediaType, dir.FullName, deadline.Token);
                var plan = await bands.PrepareAsync(pages, media, dir.FullName, deadline.Token);
                var results = new List<string>();
                foreach (var page in plan.GroupBy(part => part.Page))
                {
                    var readings = new List<string>();
                    foreach (var part in page)
                    {
                        var prepared = await File.ReadAllBytesAsync(part.Path, deadline.Token);
                        var reading = await SendAsync(job.Id, prepared, part.Media, page.Key, deadline.Token);
                        if (reading.Json is null) return reading;
                        readings.Add(reading.Json);
                    }
                    var merged = SpurResult.MergeBands(readings, job.Id, page.Key);
                    if (merged.Json is null) return merged;
                    results.Add(merged.Json);
                }
                return SpurResult.Combine(results, job.Id);
            }
            finally { dir.Delete(recursive: true); }
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested) { return new(Error: Errors.DocumentTimeout.Code); }
        catch (RenderFailure ex) { return new(Error: ex.Message); }
        catch (System.ComponentModel.Win32Exception) { return new(Error: Errors.RendererUnavailable.Code); }
        catch (IOException) { return new(Error: Errors.DocumentNotRenderable.Code); }
        catch (UnauthorizedAccessException) { return new(Error: Errors.DocumentNotRenderable.Code); }
    }

    /// <summary>Submit one bounded page or the known local CSV and validate the reader reply.</summary>
    /// <param name="id">Document receipt identifier.</param>
    /// <param name="bytes">Verified input for this call.</param>
    /// <param name="media">MIME type of the supplied bytes.</param>
    /// <param name="page">One-based printed page; zero selects the local fixture.</param>
    /// <param name="stopping">Job cancellation signal.</param>
    private async Task<ReadOutcome> SendAsync(Guid id, byte[] bytes, string media, int page, CancellationToken stopping)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add(Wire.DocumentHeader, id.ToString("D"));
        request.Headers.Add(Wire.ReaderContractHeader, Wire.Version);
        if (page > 0) request.Headers.Add(Wire.PageHeader, page.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(media);
        try
        {
            using var response = await http.SendAsync(request, stopping);
            if (response.StatusCode != HttpStatusCode.OK)
                return await ReaderProblems.ClassifyAsync(response, page > 0, stopping);
            if (response.Content.Headers.ContentType?.MediaType != Media.Json)
                return new(Error: Errors.InvalidReaderResult.Code);
            var body = await response.Content.ReadAsByteArrayAsync(stopping);
            return page > 0 ? SpurResult.Validate(body, id, page) : FixtureResult.Validate(body, id);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        { return new(Error: Errors.ReaderTimeout.Code, Retryable: true); }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        { return new(Error: Errors.InvalidReaderResult.Code); }
        catch (HttpRequestException) { return new(Error: Errors.ReaderUnavailable.Code, Retryable: true); }
    }
}
