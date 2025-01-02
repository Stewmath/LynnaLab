using System.IO;

namespace LynnaLab;

public interface IBackend
{
    // ================================================================================
    // Properties
    // ================================================================================
    public bool Exited { get; }
    public bool CloseRequested { get; set; }


    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Called before ImGui rendering occurs
    /// </summary>
    public void HandleEvents(float deltaTime);

    /// <summary>
    /// Called after main imgui rendering occurs, this will draw the results of that
    /// </summary>
    public void Render();

    /// <summary>
    /// Closes the window.
    /// </summary>
    public void Close();

    /// <summary>
    /// Creates an image from the given bitmap. The image tracks changes from the bitmap as long as
    /// the bitmap is not Disposed.
    /// </summary>
    public Image ImageFromBitmap(Bitmap bitmap);
    public Image ImageFromFile(string filename);
    public Image CreateImage(int width, int height);

    public void RecreateFontTexture();
    public void SetIcon(string path);
}

public enum Interpolation
{
    Nearest = 0,
    Bicubic = 1,

    Count
}
