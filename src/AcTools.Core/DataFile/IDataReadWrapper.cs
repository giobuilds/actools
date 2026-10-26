#nullable disable

namespace AcTools.DataFile {
    public interface IDataReadWrapper {
        bool IsEmpty { get; }

        bool IsPacked { get; }

        T GetFile<T>(string name) where T : IDataFile, new();

        string GetData(string name);
    }
}