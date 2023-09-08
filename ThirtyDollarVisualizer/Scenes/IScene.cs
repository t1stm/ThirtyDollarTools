namespace ThirtyDollarVisualizer.Scenes;

public interface IScene
{
    public void Init(Manager manager);
    public void Start();
    public void Render();
    public void Update();
    public void Resize(int w, int h);
    public void Close();
}