#nullable disable

namespace AcTools.DataFile {
    public interface IDataFile {
        string Name { get; }

        string Filename { get; }

        void Initialize(IDataReadWrapper data, string name, string filename);
    }
}