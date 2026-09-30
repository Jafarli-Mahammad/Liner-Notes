using LinerNotes.Domain.Catalog;
using Xunit;

namespace LinerNotes.Domain.Tests.Catalog;

public sealed class WeightedTagVectorTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var v1 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["black metal"] = 0.8,
            ["atmospheric"] = 0.5
        });

        var similarity = v1.CosineSimilarity(v1);

        Assert.Equal(1.0, similarity, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var v1 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["black metal"] = 1.0
        });

        var v2 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["synthpop"] = 1.0
        });

        var similarity = v1.CosineSimilarity(v2);

        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_EmptyVector_ReturnsZero()
    {
        var v1 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["jazz"] = 1.0
        });

        var similarity = v1.CosineSimilarity(WeightedTagVector.Empty);

        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void DotProduct_CalculatesCorrectSum()
    {
        var v1 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["post-rock"] = 2.0,
            ["ambient"] = 3.0
        });

        var v2 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["post-rock"] = 4.0,
            ["ambient"] = 5.0,
            ["drone"] = 10.0
        });

        // 2*4 + 3*5 = 8 + 15 = 23
        var dot = v1.DotProduct(v2);

        Assert.Equal(23.0, dot);
    }

    [Fact]
    public void TopOverlappingTags_OrdersByContributionProductDescending()
    {
        var v1 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["ambient"] = 1.0,
            ["drone"] = 0.5,
            ["techno"] = 0.2
        });

        var v2 = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["ambient"] = 0.8,
            ["drone"] = 0.9,
            ["techno"] = 0.1
        });

        // ambient: 1.0 * 0.8 = 0.80
        // drone:   0.5 * 0.9 = 0.45
        // techno:  0.2 * 0.1 = 0.02
        var overlaps = v1.GetTopOverlappingTags(v2, limit: 3);

        Assert.Equal(3, overlaps.Count);
        Assert.Equal("ambient", overlaps[0].TagName);
        Assert.Equal(0.80, overlaps[0].Contribution, precision: 6);
        Assert.Equal("drone", overlaps[1].TagName);
        Assert.Equal(0.45, overlaps[1].Contribution, precision: 6);
        Assert.Equal("techno", overlaps[2].TagName);
        Assert.Equal(0.02, overlaps[2].Contribution, precision: 6);
    }

    [Fact]
    public void Tag_Normalization_EnsuresCaseInsensitivity()
    {
        var tagUpper = Tag.Create("Black Metal");
        var tagLower = Tag.Create("black metal");
        var tagTrim = Tag.Create("  black metal  ");

        Assert.Equal(tagUpper, tagLower);
        Assert.Equal(tagUpper, tagTrim);
        Assert.Equal(tagUpper.GetHashCode(), tagLower.GetHashCode());
    }
}
