using System.Text.Json;
using LinerNotes.Application.Features.Discovery;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class TasteVocabularyTests
{
    [Fact]
    public void VocabularyHasMoreThanTwoHundredUniqueCanonicalTagsAndAliasesResolve()
    {
        Assert.True(TasteVocabulary.Entries.Count > 200);
        Assert.Equal(TasteVocabulary.Entries.Count, TasteVocabulary.Entries.Select(t => t.Id).Distinct().Count());
        Assert.Equal("hip hop", TasteVocabulary.Canonicalize("HIP-HOP"));
        Assert.Equal("r&b", TasteVocabulary.Canonicalize("Rhythm and Blues"));
        Assert.Equal("drum and bass", TasteVocabulary.Canonicalize("DNB"));
        Assert.All(TasteVocabulary.Entries, tag => Assert.Equal(tag.Id, TasteVocabulary.Canonicalize(tag.Id)));
    }

    [Fact]
    public void SearchIsPagedCategorizedAndUnknownTextDoesNotBecomeAProviderSeed()
    {
        var first = JsonSerializer.SerializeToElement(TasteVocabulary.Search("", "genre", 0, 24));
        var second = JsonSerializer.SerializeToElement(TasteVocabulary.Search("", "genre", 24, 24));
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Empty(first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("Id").GetString())
            .Intersect(second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("Id").GetString())));
        Assert.Throws<ArgumentException>(() => TasteVocabulary.Canonicalize("not a maintained taste tag"));
        Assert.Throws<ArgumentException>(() => TasteVocabulary.Search("", null, -1, 24));
    }
}
