public interface ISavableObject
{
    string SaveId { get; }
    string CaptureState();
    void RestoreState(string stateJson);
}