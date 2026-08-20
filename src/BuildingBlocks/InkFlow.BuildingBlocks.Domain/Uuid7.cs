namespace InkFlow.BuildingBlocks.Domain;

public static class Uuid7
{
    public static Guid New() => Guid.CreateVersion7();
}
