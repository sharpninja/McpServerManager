using FluentAssertions;
using McpServerManager.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.Core.Tests.Services.Infrastructure;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly FileSystemService _sut = new();
    private readonly string _tempDir;

    public FileSystemServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FsServiceTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // --- FileExists ---

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        var path = CreateTempFile("exists.txt", "hello");
        _sut.FileExists(path).Should().BeTrue();
    }

    [Fact]
    public void FileExists_NonExistentFile_ReturnsFalse()
    {
        _sut.FileExists(Path.Combine(_tempDir, "nope.txt")).Should().BeFalse();
    }

    // --- DirectoryExists ---

    [Fact]
    public void DirectoryExists_ExistingDirectory_ReturnsTrue()
    {
        _sut.DirectoryExists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_NonExistentDirectory_ReturnsFalse()
    {
        _sut.DirectoryExists(Path.Combine(_tempDir, "nope")).Should().BeFalse();
    }

    // --- ReadAllText / WriteAllText ---

    [Fact]
    public void WriteAllText_ThenReadAllText_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "rw.txt");
        _sut.WriteAllText(path, "round-trip");
        _sut.ReadAllText(path).Should().Be("round-trip");
    }

    // --- ReadAllTextAsync / WriteAllTextAsync ---

    [Fact]
    public async Task WriteAllTextAsync_ThenReadAllTextAsync_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "rw_async.txt");
        await _sut.WriteAllTextAsync(path, "async-content");
        var result = await _sut.ReadAllTextAsync(path);
        result.Should().Be("async-content");
    }

    // --- ReadAllLines ---

    [Fact]
    public void ReadAllLines_MultipleLines_ReturnsArray()
    {
        var path = CreateTempFile("lines.txt", "a\nb\nc");
        _sut.ReadAllLines(path).Should().Equal("a", "b", "c");
    }

    // --- MoveFile ---

    [Fact]
    public void MoveFile_ExistingFile_MovesToDestination()
    {
        var src = CreateTempFile("src.txt", "data");
        var dst = Path.Combine(_tempDir, "dst.txt");
        _sut.MoveFile(src, dst);
        File.Exists(src).Should().BeFalse();
        File.ReadAllText(dst).Should().Be("data");
    }

    // --- DeleteFile ---

    [Fact]
    public void DeleteFile_ExistingFile_RemovesFile()
    {
        var path = CreateTempFile("del.txt", "bye");
        _sut.DeleteFile(path);
        File.Exists(path).Should().BeFalse();
    }

    // --- GetLastWriteTimeUtc ---

    [Fact]
    public void GetLastWriteTimeUtc_ExistingFile_ReturnsRecentTime()
    {
        var path = CreateTempFile("time.txt", "t");
        var time = _sut.GetLastWriteTimeUtc(path);
        time.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    // --- EnumerateFiles ---

    [Fact]
    public void EnumerateFiles_MatchingPattern_ReturnsEntries()
    {
        CreateTempFile("a.txt", "");
        CreateTempFile("b.txt", "");
        CreateTempFile("c.log", "");

        var entries = _sut.EnumerateFiles(_tempDir, "*.txt", recursive: false).ToList();
        entries.Should().HaveCount(2);
        entries.Select(e => e.Name).Should().Contain("a.txt").And.Contain("b.txt");
        entries.Should().AllSatisfy(e => e.Extension.Should().Be(".txt"));
        entries.Should().AllSatisfy(e => e.IsDirectory.Should().BeFalse());
    }

    // --- EnumerateDirectories ---

    [Fact]
    public void EnumerateDirectories_SubDirs_ReturnsEntries()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub2"));

        var entries = _sut.EnumerateDirectories(_tempDir, "*", recursive: false).ToList();
        entries.Should().HaveCount(2);
        entries.Select(e => e.Name).Should().Contain("sub1").And.Contain("sub2");
    }

    // --- GetFullPath ---

    [Fact]
    public void GetFullPath_RelativePath_ReturnsAbsolute()
    {
        var result = _sut.GetFullPath("relative.txt");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    // --- GetDirectoryName ---

    [Fact]
    public void GetDirectoryName_FilePath_ReturnsParent()
    {
        var result = _sut.GetDirectoryName(Path.Combine(_tempDir, "child.txt"));
        result.Should().Be(_tempDir);
    }

    // --- CombinePath ---

    [Fact]
    public void CombinePath_MultipleSegments_CombinesCorrectly()
    {
        var result = _sut.CombinePath("a", "b", "c.txt");
        result.Should().Be(Path.Combine("a", "b", "c.txt"));
    }
}
