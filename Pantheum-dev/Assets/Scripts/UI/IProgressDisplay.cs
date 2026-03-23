namespace Pantheum.UI
{
    /// <summary>t is 0..1 normalized build progress.</summary>
    public interface IProgressDisplay
    {
        void UpdateProgress(float t);
    }
}
