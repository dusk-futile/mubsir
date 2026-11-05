using System.Text;
using Mubsir.Core.Reading;

namespace Mubsir.Core.Tests.Reading;

public sealed class ReadingPositionsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mubsir-positions").FullName;

    private string Store => Path.Combine(_dir, "data", "positions.json");

    private string Book(string name, string text)
    {
        string path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void A_renamed_book_keeps_its_place()
    {
        string path = Book("scan0001.pdf", "the book");
        new ReadingPositions(Store).Set(path, 34, 120);

        string renamed = Path.Combine(_dir, "Introducing Phonology.pdf");
        File.Move(path, renamed);

        var place = new ReadingPositions(Store).Get(renamed);
        Assert.Equal(34, place?.Page);
        Assert.Equal(120, place?.Offset);
    }

    [Fact]
    public void A_book_moved_to_another_folder_keeps_its_place()
    {
        string path = Book("downloads/book.txt", "the book");
        new ReadingPositions(Store).Set(path, 7, 3);

        string moved = Path.Combine(_dir, "library", "book.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        File.Move(path, moved);

        Assert.Equal(7, new ReadingPositions(Store).Get(moved)?.Page);
    }

    [Fact]
    public void A_renamed_book_in_the_same_session_keeps_its_place_and_leaves_one_entry()
    {
        string path = Book("a.txt", "the book");
        var positions = new ReadingPositions(Store);
        positions.Set(path, 2, 0);
        string renamed = Path.Combine(_dir, "b.txt");
        File.Move(path, renamed);

        Assert.Equal(2, positions.Get(renamed)?.Page);
        positions.Set(renamed, 3, 0);
        Assert.Equal(3, positions.Get(renamed)?.Page);
        Assert.Single(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Store)).RootElement.GetProperty("items").EnumerateObject());
    }

    [Fact]
    public void An_edited_book_keeps_its_place_by_path()
    {
        string path = Book("notes.txt", "version one");
        new ReadingPositions(Store).Set(path, 5, 0);
        File.WriteAllText(path, "version two, now longer");

        var positions = new ReadingPositions(Store);
        Assert.Equal(5, positions.Get(path)?.Page);

        positions.Set(path, 6, 0);
        Assert.Equal(6, new ReadingPositions(Store).Get(path)?.Page);
        Assert.Single(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Store)).RootElement.GetProperty("items").EnumerateObject());
    }

    [Fact]
    public void A_bookmark_from_an_older_version_is_found_and_upgraded()
    {
        string path = Book("old.txt", "the book");
        Directory.CreateDirectory(Path.GetDirectoryName(Store)!);
        string oldKey = System.Text.Json.JsonSerializer.Serialize(Path.GetFullPath(path).ToUpperInvariant());
        File.WriteAllText(Store, "{\"version\":1,\"items\":{" + oldKey + ":{\"page\":12,\"offset\":9,\"updatedAt\":\"2026-01-01T00:00:00+00:00\"}}}");

        var positions = new ReadingPositions(Store);
        Assert.Equal(12, positions.Get(path)?.Page);
        Assert.Equal(9, positions.Get(path)?.Offset);

        positions.Set(path, 13, 0);
        string renamed = Path.Combine(_dir, "new.txt");
        File.Move(path, renamed);
        Assert.Equal(13, new ReadingPositions(Store).Get(renamed)?.Page);
    }

    [Fact]
    public void Different_books_have_different_places()
    {
        string one = Book("one.txt", "first book");
        string two = Book("two.txt", "second book");
        var positions = new ReadingPositions(Store);
        positions.Set(one, 1, 0);
        positions.Set(two, 2, 0);
        Assert.Equal(1, positions.Get(one)?.Page);
        Assert.Equal(2, positions.Get(two)?.Page);
    }

    [Fact]
    public void A_book_with_no_place_returns_null()
    {
        Assert.Null(new ReadingPositions(Store).Get(Book("new.txt", "never read")));
    }

    [Fact]
    public void A_broken_store_is_treated_as_empty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Store)!);
        File.WriteAllText(Store, "{not json");
        string path = Book("x.txt", "the book");
        var positions = new ReadingPositions(Store);
        Assert.Null(positions.Get(path));
        positions.Set(path, 4, 0);
        Assert.Equal(4, new ReadingPositions(Store).Get(path)?.Page);
    }

    [Fact]
    public void A_book_deleted_while_being_read_still_saves()
    {
        string path = Book("gone.txt", "the book");
        var positions = new ReadingPositions(Store);
        positions.Set(path, 1, 0);
        File.Delete(path);
        positions.Set(path, 2, 0);
        Assert.Equal(2, positions.Get(path)?.Page);
    }

    [Fact]
    public void The_fingerprint_changes_with_the_start_the_end_and_the_length()
    {
        byte[] book = Encoding.UTF8.GetBytes(new string('a', 3 * 1024 * 1024));
        string baseline = Print(book);
        Assert.Equal(baseline, Print(book));

        byte[] start = (byte[])book.Clone();
        start[0] = (byte)'b';
        Assert.NotEqual(baseline, Print(start));

        byte[] end = (byte[])book.Clone();
        end[^1] = (byte)'b';
        Assert.NotEqual(baseline, Print(end));

        Assert.NotEqual(baseline, Print(book[..^1]));
        Assert.NotEqual(Print([]), Print([0]));
    }

    [Fact]
    public void The_fingerprint_of_a_missing_file_is_null()
    {
        Assert.Null(FileFingerprint.Of(Path.Combine(_dir, "nothing.pdf")));
    }

    private static string Print(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return FileFingerprint.Of(stream);
    }
}
