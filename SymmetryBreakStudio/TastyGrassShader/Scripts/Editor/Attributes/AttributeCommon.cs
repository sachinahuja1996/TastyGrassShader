namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    public static class AttributeCommon
    {
        public static string ResolveRelativePath(string fullPropertyPath, string attributeName)
        {
            int dotIndex = fullPropertyPath.LastIndexOf('.');
            if (dotIndex > 0)
            {
                /* if this is a type which is nested into another, we need to adjust the path first, so that
                   FindProperty() will correctly. */
                fullPropertyPath = fullPropertyPath[..(dotIndex + 1)];
                fullPropertyPath += attributeName;
            }
            else
            {
                fullPropertyPath = attributeName;
            }

            return fullPropertyPath;
        }
    }
}