using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Xunit;

namespace Districauchos.Core.Tests;

public class ProductImagesTests
{
    private static CatalogItem Item(string oem, params string[] aliases) =>
        new(oem, aliases, oem.ToLowerInvariant(), "T", "", null, null, true, 1, 1, null, true, "A", [], []);

    [Theory]
    [InlineData("8'94408'840'1", "8944088401")]
    [InlineData("48815-42090", "4881542090")]
    [InlineData("ur5634470b", "UR5634470B")]
    [InlineData("COD:ABC123", "ABC123")]
    [InlineData("COD:dh'40", "DH40")]
    [InlineData("COD:'", null)]
    public void Key_keeps_only_letters_and_digits(string oem, string? expected) =>
        Assert.Equal(expected, ProductImages.Key(oem));

    [Fact]
    public void Attach_uses_codes_only_for_items_without_oem()
    {
        var catalog = new CatalogBuildResult();
        catalog.Items.AddRange([
            new CatalogItem("COD:DB001", [], "db001", "T", "", null, null, true, 1, 1, null, true, "A", ["DB001"], ["C2058"]),
            new CatalogItem("96180071", [], "x", "T", "", null, null, true, 1, 1, null, true, "A", ["X'1"], []),
            new CatalogItem("93378096", [], "y", "T", "", null, null, true, 1, 1, null, true, "A", ["Y1"], ["Y2"]),
        ]);
        var photos = new Dictionary<string, long> { ["C2058"] = 1, ["X1"] = 2, ["93378096"] = 3, ["Y1"] = 4 };

        Assert.Equal(2, ProductImages.Attach(catalog, photos));
        Assert.Equal("/fotos/C2058.webp?v=1", catalog.Items[0].Image);
        Assert.Null(catalog.Items[1].Image); // con OEM no se usa el código del local
        Assert.Equal("/fotos/93378096.webp?v=3", catalog.Items[2].Image);
    }

    [Fact]
    public void Attach_matches_oem_or_alias_and_counts()
    {
        var catalog = new CatalogBuildResult();
        catalog.Items.AddRange([Item("8'94408'840'1"), Item("8944088401"), Item("111", "48815-42090"), Item("999"), Item("COD:X1")]);
        var photos = new Dictionary<string, long> { ["8944088401"] = 10, ["4881542090"] = 20, ["X1"] = 30 };

        var n = ProductImages.Attach(catalog, photos);

        Assert.Equal(4, n);
        Assert.Equal("/fotos/8944088401.webp?v=10", catalog.Items[0].Image);
        Assert.Equal("/fotos/8944088401.webp?v=10", catalog.Items[1].Image);
        Assert.Equal("/fotos/4881542090.webp?v=20", catalog.Items[2].Image);
        Assert.Null(catalog.Items[3].Image);
        Assert.Equal("/fotos/X1.webp?v=30", catalog.Items[4].Image);
        Assert.Equal(4, catalog.Stats.ItemsWithImage);
    }
}
