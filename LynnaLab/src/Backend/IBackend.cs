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

    // Image creation functions
    public Image ImageFromBitmap(Bitmap bitmap,
                                 Interpolation interpolation = Interpolation.Nearest);
    /// <summary>
    /// Creates an image which is a reference to another image. Contents will be the same but
    /// interpolation & alpha values used for rendering can be different.
    /// </summary>
    public Image ImageFromImage(Image image, Interpolation interpolation, float alpha);
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
