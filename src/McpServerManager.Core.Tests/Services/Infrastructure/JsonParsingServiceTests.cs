using System.Text.Json.Nodes;
using FluentAssertions;
using McpServerManager.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.Core.Tests.Services.Infrastructure;

public sealed class JsonParsingServiceTests
{
    private readonly JsonParsingService _sut = new();

    // --- ParseToTree ---

    [Fact]
    public void ParseToTree_SimpleObject_ReturnsCorrectNodeCount()
    {
        var result = _sut.ParseToTree("""{"a":1,"b":2}""");
        result.RootNode.Should().BeOfType<JsonObject>();
        // root(1) + a(1) + b(1) = 3
        result.NodeCount.Should().Be(3);
    }

    [Fact]
    public void ParseToTree_NestedObject_CountsAllNodes()
    {
        var result = _sut.ParseToTree("""{"a":{"b":1}}""");
        // root(1) + a-obj(1) + b(1) = 3
        result.NodeCount.Should().Be(3);
    }

    [Fact]
    public void ParseToTree_Array_CountsElements()
    {
        var result = _sut.ParseToTree("""[1,2,3]""");
        result.RootNode.Should().BeOfType<JsonArray>();
        // array(1) + 3 elements = 4
        result.NodeCount.Should().Be(4);
    }

    [Fact]
    public void ParseToTree_EmptyObject_CountsOne()
    {
        var result = _sut.ParseToTree("""{}""");
        result.NodeCount.Should().Be(1);
    }

    [Fact]
    public void ParseToTree_InvalidJson_Throws()
    {
        var act = () => _sut.ParseToTree("not json");
        act.Should().Throw<Exception>();
    }

    // --- Validate ---

    [Fact]
    public void Validate_ValidJson_ReturnsNull()
    {
        _sut.Validate("""{"key":"value"}""").Should().BeNull();
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsErrorMessage()
    {
        var result = _sut.Validate("{bad}");
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ValidArray_ReturnsNull()
    {
        _sut.Validate("[1,2,3]").Should().BeNull();
    }

    // --- PrettyPrint ---

    [Fact]
    public void PrettyPrint_CompactJson_ReturnsIndented()
    {
        var result = _sut.PrettyPrint("""{"a":1}""");
        result.Should().Contain("\n");
        result.Should().Contain("\"a\"");
    }

    [Fact]
    public void PrettyPrint_AlreadyFormatted_ReturnsFormatted()
    {
        var input = "{\n  \"a\": 1\n}";
        var result = _sut.PrettyPrint(input);
        result.Should().Contain("\"a\"");
    }
}
