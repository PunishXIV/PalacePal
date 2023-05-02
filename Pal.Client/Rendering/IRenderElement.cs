namespace Pal.Client.Rendering
{
    public interface IRenderElement
    {
        bool IsValid { get; }

        uint Color { get; set; }
    }
}
