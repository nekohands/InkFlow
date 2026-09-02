using InkFlow.Modules.Identity.Application;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class IdentityAvatarTests
{
    [TestMethod]
    public async Task Read_Accepts_Png_And_Uses_Server_Detected_Content_Type()
    {
        var bytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x01, 0x02, 0x03,
        };

        var image = await AvatarImage.ReadAsync(new MemoryStream(bytes));

        Assert.IsNotNull(image);
        Assert.AreEqual("image/png", image!.ContentType);
        CollectionAssert.AreEqual(bytes, image.Content);
    }

    [TestMethod]
    public async Task Read_Rejects_Unsupported_Content_And_Overlarge_Input()
    {
        var unsupported = await AvatarImage.ReadAsync(
            new MemoryStream("<svg></svg>"u8.ToArray()));
        var oversized = new byte[AvatarImage.MaxUploadBytes + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;

        var tooLarge = await AvatarImage.ReadAsync(new MemoryStream(oversized));

        Assert.IsNull(unsupported);
        Assert.IsNull(tooLarge);
    }
}
