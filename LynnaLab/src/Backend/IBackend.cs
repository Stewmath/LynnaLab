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
    public Image ImageFromBitmap(Bitmap bitmap,
                                 Interpolation interpolation = Interpolation.Nearest);
    /// <summary>
    /// Creates an image which is a reference to another image. Contents will be the same but
    /// interpolation & alpha values used for rendering can be different.
    /// </summary>
    public Image ImageFromImage(Image image, Interpolation interpolation, float alpha);
    public Image ImageFromFile(string filename, Interpolation interpolation);
    public Image CreateImage(int width, int height,
                             Interpolation interpolation = Interpolation.Nearest);

    public void RecreateFontTexture();
}

public enum Interpolation
{
    Nearest = 0,
    Bicubic = 1,

    Count
}
