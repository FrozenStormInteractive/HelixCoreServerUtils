namespace Perforce.P4;

public static class P4TypeMapExtensions
{
    public static void AddEntry(this TypeMap typemap, string filetype, string path)
    {
        typemap.Add(new TypeMapEntry(new FileType(filetype), path));
    }

    public static void AddEntry(this TypeMap typemap, FileType filetype, string path)
    {
        typemap.Add(new TypeMapEntry(filetype, path));
    }

    public static void AddEntry(this TypeMap typemap, BaseFileType filetype, string path)
    {
        typemap.Add(new TypeMapEntry(new FileType(filetype, FileTypeModifier.None), path));
    }

    public static void AddEntry(this TypeMap typemap, BaseFileType filetype, FileTypeModifier filetypeModifier, string path)
    {
        typemap.Add(new TypeMapEntry(new FileType(filetype, filetypeModifier), path));
    }
}
