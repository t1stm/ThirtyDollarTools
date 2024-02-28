using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarConverter.Objects;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Scenes;

public class ThreeDollarWebsite : ThirtyDollarWorkflow, IScene
{
    private ThreeDollarCamera Camera = new ThreeDollarCamera((0,0,0), 16f / 9f);
    private Manager Manager = null!;

    private int Width;
    private int Height;
    
    
    public void Init(Manager manager)
    {
        Manager = manager;
    }
    
    protected override void HandleAfterSequenceUpdate(TimedEvents events)
    {
    }

    protected override void SetSequencePlayerSubscriptions(SequencePlayer player)
    {
    }

    public void Start()
    {
        
    }

    public void Render()
    {
        
    }

    public void Update()
    {
        
    }

    public void Resize(int w, int h)
    {
        Width = w;
        Height = h;

        Camera.AspectRatio = (float)w / h;
    }

    public void Close()
    {
        
    }

    public void FileDrop(string location)
    {
        
    }

    public void Keyboard(KeyboardState state)
    {
        
    }

    public void Mouse(MouseState state)
    {
        
    }
}