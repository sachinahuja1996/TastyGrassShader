namespace SymmetryBreakStudio
{
    /// <summary>
    ///     The global status of all Tasty Grass Shader Instances (TgsInstances.cs).
    ///     Use fields like AreAllInstancesReady to implement a load screen.
    /// </summary>
    public static class TgsGlobalStatus
    {
        public static int instances { internal set; get; }

        public static int instancesReady { internal set; get; }

        public static bool areAllInstancesReady => instancesReady >= instances;
    }
}