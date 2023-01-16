namespace HelixCoreServerCtl;

public class TemporaryDirectory : IDisposable
{
    public string Path { get; }

    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}
