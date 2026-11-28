namespace HackthonSpurs.Application.Queries;

public enum ReadingAvailability { Pending, Published, Failed }

/// <summary>Carry only the fields needed to serve stored evidence.</summary>
/// <param name="Availability">Whether the stored reading is ready to serve.</param>
/// <param name="ResultJson">Published reading, if available.</param>
public sealed record DocumentResultView(ReadingAvailability Availability, string? ResultJson);
