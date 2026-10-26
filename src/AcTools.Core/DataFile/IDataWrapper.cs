#nullable disable
namespace AcTools.DataFile {
    public interface IDataWrapper : IDataReadWrapper {
        string Location { get; }

        bool Contains(string name);
        void Refresh(string name);
        void SetData(string name, string data, bool recycleOriginal = false);
        void Delete(string name, bool recycleOriginal = false);
    }
}