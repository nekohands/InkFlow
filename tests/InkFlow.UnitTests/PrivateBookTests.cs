using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class PrivateBookTests
{
    private static readonly Guid UserId = Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc");
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 19, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_Trims_Metadata_And_Binds_Owner()
    {
        var book = PrivateBook.Create(UserId, "  私人书目  ", " 作者 ", T0);

        Assert.AreEqual(UserId, book.UserId);
        Assert.AreNotEqual(Guid.Empty, book.Id);
        Assert.AreEqual("私人书目", book.Title);
        Assert.AreEqual("作者", book.Author);
        Assert.AreEqual(T0, book.CreatedAt);
        Assert.AreEqual(T0, book.UpdatedAt);
    }

    [TestMethod]
    public void Empty_Title_And_Empty_Owner_Are_Rejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PrivateBook.Create(Guid.Empty, "书", null, T0));
        Assert.ThrowsExactly<ArgumentException>(
            () => PrivateBook.Create(UserId, " ", null, T0));
    }

    [TestMethod]
    public void Optional_Author_Is_Normalized_And_Bounded()
    {
        var book = PrivateBook.Create(UserId, "书", "  ", T0);
        Assert.IsNull(book.Author);

        var tooLong = new string('a', PrivateBook.MaxAuthorLength + 1);
        Assert.ThrowsExactly<ArgumentException>(
            () => PrivateBook.Create(UserId, "书", tooLong, T0));
    }

    [TestMethod]
    public void UpdateMetadata_Changes_Only_Editable_Metadata_And_Timestamp()
    {
        var book = PrivateBook.Create(UserId, "旧名", null, T0);

        book.UpdateMetadata("新名", "新作者", T0.AddMinutes(1));

        Assert.AreEqual("新名", book.Title);
        Assert.AreEqual("新作者", book.Author);
        Assert.AreEqual(T0, book.CreatedAt);
        Assert.AreEqual(T0.AddMinutes(1), book.UpdatedAt);
    }

    [TestMethod]
    public void Invalid_Update_Does_Not_Partially_Mutate_Metadata()
    {
        var book = PrivateBook.Create(UserId, "旧名", "旧作者", T0);
        var tooLong = new string('a', PrivateBook.MaxAuthorLength + 1);

        Assert.ThrowsExactly<ArgumentException>(
            () => book.UpdateMetadata("新名", tooLong, T0.AddMinutes(1)));

        Assert.AreEqual("旧名", book.Title);
        Assert.AreEqual("旧作者", book.Author);
        Assert.AreEqual(T0, book.UpdatedAt);
    }
}
