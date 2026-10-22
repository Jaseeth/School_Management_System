using System.Text;
using SchoolManagement.API.Services;

namespace SchoolManagement.UnitTests;

public class CalendarImportTests
{
    private static IReadOnlyList<GovernmentCalendarReader.Row> Parse(string content, int year = 2026)
    { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)); return GovernmentCalendarReader.Read(stream, ".csv", year); }
    [Fact] public void SingleHolidayAndQuotedNamesAreRead() {
        var row = Assert.Single(Parse("From,Through,Name\n2026-01-01,,\"New Year, school closed\""));
        Assert.Null(row.Error); Assert.Equal(1, row.Days); Assert.Equal("New Year, school closed", row.Name);
    }
    [Fact] public void MonthLongBreakIncludesBothEndpoints() {
        var row = Assert.Single(Parse("From,Through,Name\n2026-03-01,2026-03-30,Ramadan break"));
        Assert.Null(row.Error); Assert.Equal(30, row.Days);
    }
    [Theory]
    [InlineData("2026-02-30", "2026-03-01", "Invalid date")]
    [InlineData("2026-03-02", "2026-03-01", "Reversed range")]
    [InlineData("2025-12-31", "2026-01-02", "Cross year")]
    [InlineData("2026-01-01", "", "")]
    public void InvalidRowsAreMarkedWithoutImporting(string from, string through, string name) {
        var row = Assert.Single(Parse($"From,Through,Name\n{from},{through},{name}"));
        Assert.NotNull(row.Error); Assert.Equal(0, row.Days);
    }
    [Fact] public void MissingHeadersAreRejected() => Assert.Throws<CalendarFileException>(() => Parse("Wrong,Columns\n2026-01-01,Holiday"));
    [Fact] public void EmptyFileIsRejected() => Assert.Throws<CalendarFileException>(() => Parse("From,Through,Name"));
    [Fact] public void LeapDayIsSupported() => Assert.Null(Assert.Single(Parse("Date,Name\n2028-02-29,Holiday", 2028)).Error);
    [Fact] public void UnsupportedUploadsAreRejected() {
        using var stream = new MemoryStream(); Assert.Throws<CalendarFileException>(() => GovernmentCalendarReader.Read(stream, ".pdf", 2026));
    }
    [Fact] public void PromotionUndoPreservesGraduationAndEnrollmentState() {
        var state = new PromotionUndoState(3, true, new DateOnly(2026,12,31), 2,
            [new(1,2,3,true,true)], new(3,"Repeat",4,new DateTime(2026,1,1)), 9);
        var restored = PromotionUndoState.Parse(state.Serialize());
        Assert.Equal(state.SchoolClassId, restored.SchoolClassId); Assert.Equal(state.GraduationDate, restored.GraduationDate);
        Assert.Equal(state.Enrollments, restored.Enrollments); Assert.Equal(state.Exception, restored.Exception);
    }
}
