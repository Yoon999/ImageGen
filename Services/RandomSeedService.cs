namespace ImageGen.Services;

public static class RandomSeedService
{
    public static long NextSeed()
    {
        return Random.Shared.NextInt64(1, 9999999999);
    }
}
