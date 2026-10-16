using System.Globalization;
using LinerNotes.Domain.Catalog;
using Xunit;

namespace LinerNotes.Domain.Tests.Catalog;

public sealed class CatalogTextTests
{
    [Theory]
    [InlineData("Björk", "bjork")]
    [InlineData("İşıq ƏLİF", "isiq elif")]
    [InlineData("東京", "東京")]
    [InlineData("AC/DC", "ac dc")]
    [InlineData("STRAẞE", "strasse")]
    [InlineData("!!!", "!!!")]
    [InlineData("†††", "†††")]
    public void SearchNormalizationIsCultureIndependent(string input, string expected)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, CatalogText.Normalize(input));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(expected, CatalogText.Normalize(input));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }
}
